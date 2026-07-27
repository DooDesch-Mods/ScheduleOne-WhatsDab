using MelonLoader;
using Sideload.Api;
using WhatsDab.Chat;

[assembly: MelonInfo(typeof(WhatsDab.Core), "WhatsDab", "1.0.0", "DooDesch")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace WhatsDab
{
    /// <summary>
    /// The whole mod. Registering the app is one call; after that the interface lives in
    /// Assets/whatsdab/{index.html, app.css, app.js} and this file only supplies data.
    ///
    /// Note what is NOT here: no Unity type, no uGUI, no layout arithmetic, no reference to Sideload itself. If
    /// Sideload is not installed the shim binds nothing and every call below is a no-op, so this mod can be shipped
    /// with a soft dependency rather than a hard one.
    /// </summary>
    public class Core : MelonMod
    {
        internal static MelonLogger.Instance Log;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;

            AppHandle app = Apps.Register(
                id: "whatsdab",
                bundlePrefix: "WhatsDab.Assets.whatsdab",
                title: "WhatsDab",
                iconLabel: "WhatsDab")
                // Both, opening in landscape: the two-pane split is the better read when there is room for it, and
                // naming portrait as well is what lets the player turn the phone. Everything that follows from the
                // turn is in app.css - see the @media block there.
                .Orientation("landscape", "portrait");

            ChatBackend.Install(app);

            Log.Msg(Apps.Available
                ? "[WhatsDab] registered with Sideload."
                : "[WhatsDab] Sideload is not loaded yet - registration is queued and replays when it appears.");
        }

        public override void OnUpdate() => ChatBackend.Tick();
    }
}
