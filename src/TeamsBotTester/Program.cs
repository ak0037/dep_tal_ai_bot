// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.TeamsBot
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Gdk;
    using Gtk;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Imaging;
    using PsiEncodedImage = Microsoft.Psi.Imaging.EncodedImage;
    using PsiImage = Microsoft.Psi.Imaging.Image;

    /// <summary>
    /// Teams Bot test runner.
    /// </summary>
    public class Program
    {
        private static PsiImporter OpenStore(Pipeline pipeline)
        {
            // open your recorded bot data store
            return PsiStore.Open(pipeline, "<insert store name>", "<insert store path>");
        }

        [STAThread]
        private static void Main()
        {
            Application.Init();
            var bld = new Builder();
            bld.AddFromString(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<interface>" +
                    "<requires lib=\"gtk+\" version=\"3.12\"/>" +
                    "<object class=\"GtkWindow\" id=\"window\">" +
                        "<child>" +
                            "<object class=\"GtkImage\" id=\"image\">" +
                                "<property name=\"width_request\">1080</property>" +
                                "<property name=\"height_request\">768</property>" +
                                "<property name=\"visible\">True</property>" +
                                "<property name=\"can_focus\">False</property>" +
                                "<property name=\"stock\">gtk-missing-image</property>" +
                            "</object>" +
                        "</child>" +
                    "</object>" +
                "</interface>");
            var win = (Gtk.Window)bld.GetObject("window");
            win.DeleteEvent += (_, __) => Application.Quit();
            var image = (Gtk.Image)bld.GetObject("image");
            win.Show();

            new Thread(new ThreadStart(() =>
            {
                using (var pipeline = Pipeline.Create())
                {
                    var store = OpenStore(pipeline);

                    var participantVideoStreams = new List<IProducer<(string, Shared<PsiImage>)>>();
                    var participantAudioStreams = new List<IProducer<(string, AudioBuffer)>>();

                    foreach (var streamMetadata in store.AvailableStreams)
                    {
                        var subNames = streamMetadata.Name.Split('.');
                        if (subNames.Length == 3 && subNames[0] == "Participants" && subNames[2] == "Video")
                        {
                            participantVideoStreams.Add(store
                                .OpenStream<Shared<PsiEncodedImage>>(streamMetadata.Name)
                                .Decode(DeliveryPolicy.LatestMessage)
                                .Select(img => (subNames[1], img), DeliveryPolicy.LatestMessage));
                        }

                        if (subNames.Length == 3 && subNames[0] == "Participants" && subNames[2] == "Audio")
                        {
                            participantAudioStreams.Add(store
                                .OpenStream<AudioBuffer>(streamMetadata.Name)
                                .Select(ab => (subNames[1], ab)));
                        }
                    }

                    var video = Microsoft.Psi.Operators.Merge(participantVideoStreams, DeliveryPolicy.LatestMessage)
                        .Select(message => new Dictionary<string, (Shared<PsiImage>, DateTime)>() { { message.Data.Item1, (message.Data.Item2, message.OriginatingTime) } });

                    var audio = Microsoft.Psi.Operators.Merge(participantAudioStreams)
                        .Select(message => new Dictionary<string, (AudioBuffer, DateTime)>() { { message.Data.Item1, (message.Data.Item2, message.OriginatingTime) } });

                    pipeline.RunAsync();

                    Console.WriteLine("Press any key and close main window to exit...");
                    Console.ReadKey();
                    Console.WriteLine("Done");
                }
            }))
            { IsBackground = true }.Start();

            Application.Run();
            bld.Dispose();
        }
    }
}
