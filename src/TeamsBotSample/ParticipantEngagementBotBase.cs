// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.TeamsBot
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.ServiceModel.Channels;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Components;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Interop.Format;
    using Microsoft.Psi.Interop.Transport;
    using MsgPack;
    using MsgPack.Serialization;
    using NetMQ;
    using NetMQ.Sockets;
    using PsiImage = Microsoft.Psi.Imaging.Image;

    /// <summary>
    /// Represents a participant engagement component base class.
    /// </summary>
    public abstract class ParticipantEngagementBotBase : Subpipeline, ITeamsBot
    {
        /// <summary>
        /// Acoustic log energy threshold used for voice activity detection.
        /// </summary>
        protected const float EnergyThreshold = 8.0f;

        /// <summary>
        /// Video thumbnail scale relative to window size.
        /// </summary>
        protected const double ThumbnailWindowScale = 0.25;

        /// <summary>
        /// Video frame margin in pixels.
        /// </summary>
        protected const double FrameMarginWindowScale = 0.03;

        /// <summary>
        /// Video image border thickness in pixels.
        /// </summary>
        protected const int ImageBorderThickness = 4;

        private readonly Connector<Dictionary<string, (AudioBuffer, DateTime)>> audioInConnector;
        private readonly Connector<Dictionary<string, (AudioBuffer, DateTime)>> audioProcessingConnector;
        private readonly Connector<Dictionary<string, (Shared<PsiImage>, DateTime)>> videoProcessingConnector;
        private readonly Connector<Dictionary<string, (Shared<PsiImage>, DateTime)>> screenProcessingConnector;
        private readonly Connector<Dictionary<string, (Shared<PsiImage>, DateTime)>> videoInConnector;
        private readonly Connector<Dictionary<string, (Shared<PsiImage>, DateTime)>> screenInConnector;

        private readonly Connector<Shared<PsiImage>> videoOutConnector;
        private readonly Connector<AudioBuffer> audioOutConnector;
        private readonly Connector<AudioBuffer> audioReceiverConnector;

        private readonly NetMQWriter<byte[]> audioWriter;
        private readonly TimeSpan speechWindow = TimeSpan.FromSeconds(5);
        private readonly Bitmap icon;
        private readonly Color backgroundColor = Color.FromArgb(71, 71, 71);
        private readonly Brush textBrush = Brushes.Black;
        private readonly Brush emptyThumbnailBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        private readonly Brush labelBrush = Brushes.Gray;
        private readonly Font statusFont = new (FontFamily.GenericSansSerif, 12);
        private readonly Font labelFont = new (FontFamily.GenericSansSerif, 36);
        private readonly NetMQWriter<List<(string, byte[])>> videoWriter;
        private readonly NetMQWriter<List<(string, byte[])>> screenWriter;
        private NetMQSource<dynamic> audioReceiver;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParticipantEngagementBotBase"/> class.
        /// </summary>
        /// <param name="pipeline">The pipeline to add the component to.</param>
        /// <param name="interval">Interval at which to render and emit frames of the rendered visual.</param>
        /// <param name="screenWidth">Width at which to render the shared screen.</param>
        /// <param name="screenHeight">Height at which to render the shared screen.</param>
        /// <param name="callId">calid is being used. </param>
        public ParticipantEngagementBotBase(Pipeline pipeline, TimeSpan interval, int screenWidth, int screenHeight, string callId)
    : base(pipeline, "ParticipantEngagementBot")
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            this.ScreenWidth = screenWidth;
            this.ScreenHeight = screenHeight;
            this.icon = new Bitmap("./icon.png");
            this.FrameMargin = (int)(Math.Max(screenWidth, screenHeight) * FrameMarginWindowScale);

            // Generate unique port numbers based on callId
            int audioPort = this.GeneratePortFromCallId(callId, 30001);
            int videoPort = this.GeneratePortFromCallId(callId, 30002);
            int screenPort = this.GeneratePortFromCallId(callId, 30003);
            int audioReceiverPort = this.GeneratePortFromCallId(callId, 30004);

            // Create unique socket addresses based on generated ports
            string audioSocketAddress = $"tcp://127.0.0.1:{audioPort}";
            string videoSocketAddress = $"tcp://127.0.0.1:{videoPort + 1}";
            string screenSocketAddress = $"tcp://127.0.0.1:{screenPort + 2}";
            string audioReceiverSocketAddress = $"tcp://127.0.0.1:{audioReceiverPort + 3}";

            Console.WriteLine($"sockets- {audioPort} ---{screenPort} - {videoPort} - {audioReceiverPort}");
            this.audioInConnector = this.CreateInputConnectorFrom<Dictionary<string, (AudioBuffer, DateTime)>>(pipeline, nameof(this.audioInConnector));
            this.videoInConnector = this.CreateInputConnectorFrom<Dictionary<string, (Shared<PsiImage>, DateTime)>>(pipeline, nameof(this.videoInConnector));
            this.screenInConnector = this.CreateInputConnectorFrom<Dictionary<string, (Shared<PsiImage>, DateTime)>>(pipeline, nameof(this.screenInConnector));

            this.videoProcessingConnector = this.CreateOutputConnectorTo<Dictionary<string, (Shared<PsiImage>, DateTime)>>(pipeline, nameof(this.videoProcessingConnector));
            this.screenProcessingConnector = this.CreateOutputConnectorTo<Dictionary<string, (Shared<PsiImage>, DateTime)>>(pipeline, nameof(this.screenProcessingConnector));
            this.audioProcessingConnector = this.CreateOutputConnectorTo<Dictionary<string, (AudioBuffer, DateTime)>>(pipeline, nameof(this.audioProcessingConnector));

            this.videoOutConnector = this.CreateOutputConnectorTo<Shared<PsiImage>>(pipeline, nameof(this.videoOutConnector));
            this.audioOutConnector = this.CreateOutputConnectorTo<AudioBuffer>(pipeline, nameof(this.audioOutConnector));
            this.audioReceiverConnector = this.CreateInputConnectorFrom<AudioBuffer>(pipeline, nameof(this.audioReceiverConnector));

            // send audio over ZeroMQ
            this.audioWriter = new NetMQWriter<byte[]>(
                pipeline,
                "audio",
                audioSocketAddress,
                MessagePackFormat.Instance);

            // Subscribe to the audioReceiver and process incoming messages
            this.audioReceiver = new NetMQSource<dynamic>(
                pipeline,
                "audiore",
                audioReceiverSocketAddress,
                MessagePackFormat.Instance);

            this.audioReceiver.Select(audio =>
            {
                var waveFormat = WaveFormat.Create16kHz1Channel16BitPcm();
                var audioBuffer = new AudioBuffer(audio, waveFormat);

                return audioBuffer;
            }).PipeTo(this.audioReceiverConnector);

            var vdeo = this.videoInConnector.Aggregate(
            new Dictionary<string, Shared<PsiImage>>(),
            (aggregate, frames) =>
            {
                var newAggregate = new Dictionary<string, Shared<PsiImage>>();
                foreach (var frame in frames)
                {
                    if (frame.Value.Item1 != null)
                    {
                        newAggregate[frame.Key] = frame.Value.Item1.AddRef();
                    }
                }

                // Dispose old frames
                foreach (var oldFrame in aggregate.Values)
                {
                    oldFrame.Dispose();
                }

                return newAggregate;
            });

            // Process the audio data
            // var processedAudio = this.audioInConnector.Select(audioData => this.ProcessAudio(audioData));

            // processedAudio.PipeTo(this.audioOutConnector, DeliveryPolicy.LatestMessage);

            // Wire up audio processing pipeline using connectors
            this.audioInConnector.Out.PipeTo(this.audioProcessingConnector.In);

            this.audioProcessingConnector.Parallel(
                (participantId, stream) =>
                {
                    var audioStream = stream.Select(tuple => tuple.Item1);
                    var acousticFeatures = audioStream.PipeTo(new AcousticFeaturesExtractor(audioStream.Out.Pipeline));
                    var voiceActivity = acousticFeatures.LogEnergy
                        .Window(RelativeTimeInterval.Future(TimeSpan.FromMilliseconds(300)))
                        .Aggregate(
                            false,
                            (previous, values) =>
                            {
                                var startedSpeech = !previous && values.All(v => v > EnergyThreshold);
                                var continuedSpeech = previous && !values.All(v => v < EnergyThreshold);
                                return startedSpeech || continuedSpeech;
                            });
                    return stream.Join(voiceActivity, RelativeTimeInterval.Infinite).Select(x => (x.Item1, x.Item2, x.Item3));
                }, name: "VoiceActivityDetection")
            .Aggregate(
                (accumulatedAudio: new List<byte>(), lastSpeechTime: DateTime.Now, audioToSend: new byte[0]),
                (state, data) =>
                {
                    const int SilenceDurationThresholdMs = 2000; // 2 seconds
                    var currentTime = DateTime.Now;
                    bool hasSpeech = false;

                    foreach (var kvp in data)
                    {
                        Console.Write($"Boolean:- {kvp.Value.Item3}");
                        if (kvp.Value.Item3)
                        {
                            // Speech detected
                            hasSpeech = true;
                            state.lastSpeechTime = currentTime;
                        }

                        // Always accumulate audio, whether speech is detected or not
                        state.accumulatedAudio.AddRange(kvp.Value.Item1.Data);
                    }

                    // Check if we've been silent for more than 2 seconds
                    if (!hasSpeech && (currentTime - state.lastSpeechTime).TotalMilliseconds > SilenceDurationThresholdMs)
                    {
                        if (state.accumulatedAudio.Count > 0)
                        {
                            var audioToSend = state.accumulatedAudio.ToArray();
                            Console.WriteLine($"Sending accumulated audio of size: {audioToSend.Length}");

                            // Reset accumulated audio and last speech time, set audioToSend
                            return (new List<byte>(), currentTime, audioToSend);
                        }
                    }

                    // If we haven't met the conditions to send, return the current state with empty audioToSend
                    return (state.accumulatedAudio, state.lastSpeechTime, new byte[0]);
                })
            .Where(state => state.audioToSend.Length > 0)
            .Select(state => state.audioToSend)
            .PipeTo(this.audioWriter);

            this.audioReceiverConnector.Out.PipeTo(this.audioOutConnector, DeliveryPolicy.LatestMessage);

            this.videoWriter = new NetMQWriter<List<(string, byte[])>>(
                pipeline,
                "frames",
                videoSocketAddress,
                MessagePackFormat.Instance);

            this.videoInConnector.Out.PipeTo(this.videoProcessingConnector.In);

            var video = this.videoProcessingConnector.Aggregate(
                new Dictionary<string, Shared<PsiImage>>(),
                (aggregate, frames) =>
                {
                    // aggregate dictionary of participant ID -> video frame
                    foreach (var frame in frames)
                    {
                        if (aggregate.TryGetValue(frame.Key, out Shared<PsiImage> old))
                        {
                            old.Dispose();
                            aggregate.Remove(frame.Key);
                        }

                        aggregate.Add(frame.Key, frame.Value.Item1.AddRef());
                    }

                    return aggregate;
                });

            var simpleVideo = video.Select(dict =>
            {
                return dict.Select(kv =>
                {
                    var img = this.ImageToByteArray(kv.Value.Resource);
                    return (kv.Key, img);
                }).ToList();
            });

            simpleVideo.PipeTo(this.videoWriter);

            this.screenWriter = new NetMQWriter<List<(string, byte[])>>(
                pipeline,
                "screen",
                screenSocketAddress,
                MessagePackFormat.Instance);

            this.screenInConnector.Out.PipeTo(this.screenProcessingConnector.In);

            var screen = this.screenProcessingConnector.Aggregate(
                new Dictionary<string, Shared<PsiImage>>(),
                (aggregate, frames) =>
                {
                    // aggregate dictionary of participant ID -> video frame
                    foreach (var frame in frames)
                    {
                        if (aggregate.TryGetValue(frame.Key, out Shared<PsiImage> old))
                        {
                            old.Dispose();
                            aggregate.Remove(frame.Key);
                        }

                        aggregate.Add(frame.Key, frame.Value.Item1.AddRef());
                    }

                    return aggregate;
                });

            var simpleScreen = screen.Select(dict =>
            {
                return dict.Select(kv =>
                {
                    var img = this.ImageToByteArray(kv.Value.Resource);
                    return (kv.Key, img);
                }).ToList();
            });

            simpleScreen.PipeTo(this.screenWriter);
        }

        /// <inheritdoc/>
        public Receiver<Dictionary<string, (Shared<PsiImage>, DateTime)>> VideoIn => this.videoInConnector.In;

        /// <inheritdoc/>
        public Receiver<Dictionary<string, (Shared<PsiImage>, DateTime)>> ScreenShareIn => this.screenInConnector.In;

        /// <inheritdoc/>
        public Receiver<Dictionary<string, (AudioBuffer, DateTime)>> AudioIn => this.audioInConnector.In;

        /// <inheritdoc />
        public bool EnableScreenSharing => false;

        /// <inheritdoc />
        public (int Width, int Height) ScreenShareSize => (this.ScreenWidth, this.ScreenHeight);

        /// <inheritdoc />
        public Emitter<Shared<PsiImage>> ScreenShareOut => null;

        /// <inheritdoc />
        public bool EnableVideoOutput => true;

        /// <inheritdoc />
        public (int Width, int Height) VideoSize => (this.ScreenWidth, this.ScreenHeight);

        /// <inheritdoc />
        public Emitter<Shared<PsiImage>> VideoOut => this.videoOutConnector.Out;

        /// <inheritdoc />
        public bool EnableAudioOutput => true;

        /// <inheritdoc />
        public Emitter<AudioBuffer> AudioOut => this.audioOutConnector.Out;

        /// <summary>
        /// Gets hilight color used for video frames and other colored elements.
        /// </summary>
        protected Color HighlightColor { get; private set; } = Color.FromArgb(69, 47, 156);

        /// <summary>
        /// Gets pixel width of the output screen.
        /// </summary>
        protected int ScreenWidth { get; private set; }

        /// <summary>
        /// Gets pixel height of the output screen.
        /// </summary>
        protected int ScreenHeight { get; private set; }

        /// <summary>
        /// Gets margin within which to render video frame.
        /// </summary>
        protected int FrameMargin { get; private set; }

        private int GeneratePortFromCallId(string callId, int basePort)
        {
            // Hash the callId to generate a unique number
            int hash = callId.GetHashCode();

            // Ensure the port number is within the range 30000-31000
            int portRange = 1000; // 31000 - 30000 = 1000
            int port = 30000 + Math.Abs(hash % portRange);

            // Ensure the port is within the valid range (30000-31000)
            if (port < 30000 || port > 31000)
            {
                port = basePort; // Fallback to a default port if the generated port is invalid
            }

            return port;
        }

        /// <summary>
        /// Process the audio data and return the same audio data to be sent back.
        /// </summary>
        /// <param name="audioData">The received audio data.</param>
        /// <returns>The processed audio buffer.</returns>
        private AudioBuffer ProcessAudio(Dictionary<string, (AudioBuffer, DateTime)> audioData)
        {
            // For now, just return the audio buffer of the first participant
            // In a real-world scenario, you might want to mix audio from multiple participants or perform some processing
            if (audioData.Count > 0)
            {
                var firstParticipantAudio = audioData.First().Value.Item1;

                var waveFormat = WaveFormat.Create16kHz1Channel16BitPcm();
                var audioBytes = firstParticipantAudio.Data;

                // Create a new AudioBuffer to send back
                var audioBuffer = new AudioBuffer(audioBytes, waveFormat);

                return audioBuffer;
            }

            // If no audio data, return an empty buffer
            var emptyWaveFormat = WaveFormat.Create16kHz1Channel16BitPcm();
            return new AudioBuffer(new byte[0], emptyWaveFormat);
        }

        private void ProduceVideo(Dictionary<string, Shared<PsiImage>> videoFrames, DateTime originatingTime, Emitter<Shared<PsiImage>> emitter)
        {
            using var outputImage = ImagePool.GetOrCreate(this.ScreenWidth, this.ScreenHeight, PixelFormat.BGRA_32bpp);
            using (var bitmap = new Bitmap(this.ScreenWidth, this.ScreenHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.Clear(Color.Black); // Or any background color you prefer

                // Render video frames
                int x = 0;
                int y = 0;
                int frameWidth = this.ScreenWidth / 2;  // Adjust as needed
                int frameHeight = this.ScreenHeight / 2;  // Adjust as needed

                foreach (var frame in videoFrames.Values)
                {
                    if (frame != null)
                    {
                        using (var frameBitmap = frame.Resource.ToBitmap())
                        {
                            graphics.DrawImage(frameBitmap, x, y, frameWidth, frameHeight);
                        }
                    }

                    x += frameWidth;
                    if (x >= this.ScreenWidth)
                    {
                        x = 0;
                        y += frameHeight;
                    }
                }

                outputImage.Resource.CopyFrom(bitmap);
            }

            emitter?.Post(outputImage, originatingTime);
        }

        // Helper method to convert Image to byte array
        private byte[] ImageToByteArray(PsiImage image)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    // Write a version number or magic number
                    writer.Write((byte)1); // Version 1

                    // Write image metadata
                    writer.Write(BitConverter.GetBytes(image.Width));
                    writer.Write(BitConverter.GetBytes(image.Height));
                    writer.Write(BitConverter.GetBytes((int)image.PixelFormat)); // Ensure correct format

                    // Write image data
                    var imageData = new byte[image.Size];
                    image.CopyTo(imageData);
                    writer.Write(BitConverter.GetBytes(imageData.Length));
                    writer.Write(imageData);

                    // Debug print
                    // Console.WriteLine($"Serialized Width: {image.Width}, Height: {image.Height}, PixelFormat: {(int)image.PixelFormat}, DataLength: {imageData.Length}");
                }

                // Return byte array
                return stream.ToArray();
            }
        }

        private void ProcessReceivedData(byte[] audioData)
        {
            Console.WriteLine("ProcessReceivedData called");

            if (audioData == null || audioData.Length == 0)
            {
                Console.WriteLine("Error: Received data is null or empty.");
                return;
            }

            Console.WriteLine($"Received audio data, size: {audioData.Length} bytes");
            Console.WriteLine($"First 32 bytes (hex): {BitConverter.ToString(audioData.Take(32).ToArray())}");

            try
            {
                // Process the audio data directly
                this.ProcessAudioData(audioData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while processing audio data: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().FullName}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void ProcessAudioData(byte[] audioData)
        {
            // Implement your audio processing logic here
            // For example:
            // 1. Convert byte array to audio samples
            // 2. Apply any necessary audio processing
            // 3. Play the audio or send it for further analysis
            Console.WriteLine($"Processing audio data of length: {audioData.Length}");

            // Example: Convert byte array to short array (assuming 16-bit PCM audio)
            short[] audioSamples = new short[audioData.Length / 2];
            Buffer.BlockCopy(audioData, 0, audioSamples, 0, audioData.Length);

            Console.WriteLine($"Number of audio samples: {audioSamples.Length}");

            // Add your specific audio processing logic here
        }

        /// <summary>
        /// Represents a meeting participant.
        /// </summary>
        protected class Participant
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Participant"/> class.
            /// </summary>
            /// <param name="thumbnail">Video thumbnail.</param>
            /// <param name="x">Horizontal position of video thumbnail as vector from center.</param>
            /// <param name="y">Vertical position of video thumbnail as vector from center.</param>
            /// <param name="width">Width of video thumbnail as unit screen width.</param>
            /// <param name="height">Height of video thumbnail as unit screen height.</param>
            /// <param name="label">Label text.</param>
            public Participant(Shared<PsiImage> thumbnail, double x, double y, double width, double height, string label = default)
            {
                this.Thumbnail = thumbnail;
                this.X = x;
                this.Y = y;
                this.Width = width;
                this.Height = height;
                this.Label = label ?? string.Empty;
            }

            /// <summary>
            /// Gets horizontal position of video thumbnail as vector from center.
            /// </summary>
            public double X { get; }

            /// <summary>
            /// Gets vertical position of video thumbnail as vector from center.
            /// </summary>
            public double Y { get; }

            /// <summary>
            /// Gets label text.
            /// </summary>
            public string Label { get; }

            /// <summary>
            /// Gets latest video thumbnail.
            /// </summary>
            public Shared<PsiImage> Thumbnail { get; }

            /// <summary>
            /// Gets or sets width of video thumbnail as unit screen width.
            /// </summary>
            public double Width { get; set; }

            /// <summary>
            /// Gets or sets height of video thumbnail as unit screen height.
            /// </summary>
            public double Height { get; set; }

            /// <summary>
            /// Gets or sets recent (voice) activity level.
            /// </summary>
            public double Activity { get; set; }
        }
    }
}
