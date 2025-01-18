// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.TeamsBot
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using PsiImage = Microsoft.Psi.Imaging.Image;

    /// <summary>
    /// Represents a participant engagement component base class.
    /// </summary>
    public class ParticipantEngagementScaleBot : ParticipantEngagementBotBase
    {
        private readonly bool varyHeights;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParticipantEngagementScaleBot"/> class.
        /// </summary>
        /// <param name="pipeline">The pipeline to add the component to.</param>
        /// <param name="interval">Interval at which to render and emit frames of the rendered visual.</param>
        /// <param name="screenWidth">Width at which to render the shared screen.</param>
        /// <param name="screenHeight">Height at which to render the shared screen.</param>
        /// <param name="varyHeights">Whether to vary the heights of participant video frames.</param>
        /// <param name="callId">calid is being used. </param>
        public ParticipantEngagementScaleBot(Pipeline pipeline, TimeSpan interval, int screenWidth, int screenHeight, bool varyHeights, string callId)
            : base(pipeline, interval, screenWidth, screenHeight, callId)
        {
            this.varyHeights = varyHeights;
        }
    }
}
