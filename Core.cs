using MelonLoader;
using Sideload.Api;
using WhatsDab.Chat;

[assembly: MelonInfo(typeof(WhatsDab.Core), "WhatsDab", DooDesch.ModVersion.Current, "DooDesch", "https://github.com/DooDesch-Mods/ScheduleOne-WhatsDab")]
[assembly: MelonGame("TVGS", "Schedule I")]
// The Sideload.Api shim is compiled into this mod and finds its host by reflection, so nothing here REFERENCES
// Sideload.dll. Declaring it anyway is what makes the link visible from the outside: MelonLoader loads Sideload
// first, and Side Hustle's mod-policy dependency walk (which reads these attributes because assembly references
// tell it nothing) ships Sideload.dll to joiners and re-enables it in a "required mods only" profile. Optional,
// not additional - without Sideload this mod must still load and quietly do nothing, as documented below.
[assembly: MelonOptionalDependencies("Sideload")]

namespace WhatsDab
{
    /// <summary>
    /// The whole mod. Registering the app is one call; after that the interface lives in
    /// Assets/whatsdab/{index.html, app.css, app.js} and this file only supplies data.
    ///
    /// Note what is NOT here: no uGUI, no layout arithmetic, no reference to Sideload itself. If Sideload is not
    /// installed the shim binds nothing and every call below is a no-op, so the mod loads rather than throwing -
    /// it just has no app. Using it needs Sideload, which is why the package lists it as a dependency.
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

            ChatBackend.Install(app, ChooseSource());

            Log.Msg(Apps.Available
                ? "[WhatsDab] registered with Sideload."
                : "[WhatsDab] Sideload is not loaded yet - registration is queued and replays when it appears.");
        }

        /// <summary>
        /// Real conversations in a shipped build, always. A development build gets the scripted demo instead, so the
        /// whole interface - layouts, unread counting, notifications - is exercisable from a single-player save
        /// rather than needing a second machine in a lobby.
        ///
        /// Chosen once at init rather than per frame. Switching source under a running app would mean the player
        /// watching one conversation turn into a different one.
        /// </summary>
        private static IChatSource ChooseSource()
        {
#if DEBUG
            Log.Msg("[WhatsDab] development build - using the scripted demo conversation, not the lobby.");
            return new DemoSource();
#else
            return new Lobby.LobbySource();
#endif
        }

        public override void OnUpdate() => ChatBackend.Tick();
    }
}
