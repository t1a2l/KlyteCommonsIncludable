﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Packaging;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using Commons.Extensions.UI;
using Commons.Extensions;
using Commons.UI.i18n;
using Commons.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static ColossalFramework.UI.UITextureAtlas;
using static Commons.Utils.DialogControl;
using Commons.Utils.UtilitiesClasses;

namespace Commons.Interfaces
{

    public abstract class BasicIUserModSimplified<U, C> : IUserMod, ILoadingExtension, IViewStartActions
        where U : BasicIUserModSimplified<U, C>, new()
        where C : BaseController<U, C>
    {
        public abstract string SimpleName { get; }
        public virtual string IconName { get; } = $"_{CommonProperties.Acronym}_Icon";
        public virtual bool UseGroup9 => true;
        public virtual void DoLog(string fmt, params object[] args) => LogUtils.DoLog(fmt, args);
        public virtual void DoErrorLog(string fmt, params object[] args) => LogUtils.DoErrorLog(fmt, args);
        public virtual void TopSettingsUI(UIHelperExtension ext) { }

        private GameObject m_topObj;
        public Transform RefTransform => m_topObj?.transform;

        private static ulong m_modId;

        public static ulong ModId
        {
            get
            {
                if (m_modId == 0)
                {
                    m_modId = Singleton<PluginManager>.instance.GetPluginsInfo().Where((PluginManager.PluginInfo pi) =>
                 pi.assemblyCount > 0
                 && pi.isEnabled
                 && pi.GetAssemblies().Where(x => x == typeof(U).Assembly).Count() > 0
             ).Select(x => x?.publishedFileID.AsUInt64 ?? ulong.MaxValue).Min();
                }
                return m_modId;
            }
        }

        private static string m_rootFolder;

        public static string RootFolder
        {
            get
            {
                if (m_rootFolder == null)
                {
                    m_rootFolder = Singleton<PluginManager>.instance.GetPluginsInfo().Where((PluginManager.PluginInfo pi) =>
                 pi.assemblyCount > 0
                 && pi.isEnabled
                 && pi.GetAssemblies().Where(x => x == typeof(U).Assembly).Count() > 0
             ).FirstOrDefault()?.modPath;
                }
                return m_rootFolder;
            }
        }
        public string Name => $"{SimpleName} {Version}";
        public abstract string Description { get; }
        public static C Controller
        {
            get
            {
                if (controller is null && LoadingManager.instance.m_currentlyLoading)
                {
                    LogUtils.DoLog($"Trying to access controller while loading. NOT ALLOWED!\n Stacktrace:\n{Environment.StackTrace}");
                }
                return controller;
            }
            private set => controller = value;
        }

        public virtual void OnCreated(ILoading loading)
        {
            if (loading == null || (!loading.loadingComplete && !IsValidLoadMode(loading)))
            {
                Patcher.UnpatchAll();
            }
        }

        public void OnLevelLoaded(LoadMode mode)
        {
            OnLevelLoadedInherit(mode);
            OnLevelLoadingInternal();
        }

        protected virtual void OnLevelLoadedInherit(LoadMode mode)
        {
            if (IsValidLoadMode(mode))
            {
                if (!typeof(C).IsGenericType)
                {
                    m_topObj = GameObject.Find(typeof(U).Name) ?? new GameObject(typeof(U).Name);
                    Controller = m_topObj.AddComponent<C>();
                }
                SimulationManager.instance.StartCoroutine(LevelUnloadBinds());
                ShowVersionInfoPopup();
                SearchIncompatibilitiesModal();
            }
            else
            {
                LogUtils.DoWarnLog($"Invalid load mode: {mode}. The mod will not be loaded!");
                Patcher.UnpatchAll();
            }
        }

        private IEnumerator LevelUnloadBinds()
        {
            yield return 0;
            UIButton toMainMenuButton = GameObject.Find("ToMainMenu")?.GetComponent<UIButton>();
            if (toMainMenuButton != null)
            {
                toMainMenuButton.eventClick += (x, y) =>
                {
                    GameObject.FindObjectOfType<ToolsModifierControl>().CloseEverything();
                    ExtraUnloadBinds();
                };
            }
        }

        protected virtual void ExtraUnloadBinds() { }

        protected virtual void OnLevelLoadingInternal()
        {

        }

        protected virtual bool IsValidLoadMode(ILoading loading) => loading?.currentMode == AppMode.Game;
        protected virtual bool IsValidLoadMode(LoadMode mode) => mode == LoadMode.LoadGame || mode == LoadMode.LoadScenario || mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario;
        public string GeneralName => $"{SimpleName} (v{Version})";

        public void OnLevelUnloading()
        {
            Controller = null;
            Patcher.UnpatchAll();
            PatchesApply();
        }
        public virtual void OnReleased() => PluginManager.instance.eventPluginsStateChanged -= SearchIncompatibilitiesModal;

        protected void PatchesApply()
        {
            UnsubAuto();
            Patcher.PatchAll();
            OnPatchesApply();
        }

        protected virtual void OnPatchesApply() { }

        public void OnEnabled()
        {
            if (CurrentSaveVersion.value != FullVersion)
            {
                needShowPopup = true;
            }
            FileUtils.EnsureFolderCreation(CommonProperties.ModRootFolder);
            PatchesApply();
        }

        public void OnDisabled() => Patcher.UnpatchAll();

        public static string MinorVersion => MajorVersion + "." + typeof(U).Assembly.GetName().Version.Build;
        public static string MajorVersion => typeof(U).Assembly.GetName().Version.Major + "." + typeof(U).Assembly.GetName().Version.Minor;
        public static string FullVersion => MinorVersion + " r" + typeof(U).Assembly.GetName().Version.Revision;
        public static string Version
        {
            get
            {
                if (typeof(U).Assembly.GetName().Version.Minor == 0 && typeof(U).Assembly.GetName().Version.Build == 0)
                {
                    return typeof(U).Assembly.GetName().Version.Major.ToString();
                }
                if (typeof(U).Assembly.GetName().Version.Build > 0)
                {
                    return MinorVersion;
                }
                else
                {
                    return MajorVersion;
                }
            }
        }

        public bool needShowPopup;

        public static SavedBool DebugMode { get; } = new SavedBool(CommonProperties.Acronym + "_DebugMode", Settings.gameSettingsFile, false, true);
        private SavedString CurrentSaveVersion { get; } = new SavedString(CommonProperties.Acronym + "SaveVersion", Settings.gameSettingsFile, "null", true);
        public static bool IsCityLoaded => Singleton<SimulationManager>.instance.m_metaData != null;

        public static U m_instance = new U();
        public static U Instance => m_instance;

        private UIComponent m_onSettingsUiComponent;
        private static C controller;

        public void OnSettingsUI(UIHelperBase helperDefault)
        {

            m_onSettingsUiComponent = new UIHelperExtension((UIHelper)helperDefault).Self ?? m_onSettingsUiComponent;

            if (Locale.Get(TLMLocaleManager.m_defaultModControllingKey) == CommonProperties.ModName)
            {
                if (GameObject.FindObjectOfType<TLMLocaleManager>() is null)
                {
                    MonoUtils.CreateElement<TLMLocaleManager>(new GameObject(typeof(U).Name).transform);
                    if (Locale.GetUnchecked(TLMLocaleManager.m_defaultTestKey) != TLMLocaleManager.m_defaultTestValue)
                    {
                        LogUtils.DoErrorLog("CAN'T LOAD LOCALE!!!!!");
                    }
                    LocaleManager.eventLocaleChanged += TLMLocaleManager.ReloadLanguage;
                }
            }
            foreach (string lang in TLMLocaleManager.locales)
            {
                string content = ResourceLoader.LoadResourceString($"UI.i18n.{lang}.properties");
                FileUtils.EnsureFolderCreation($"{TLMLocaleManager.m_translateFilesPath}{lang}");
                if (content != null)
                {
                    File.WriteAllText($"{TLMLocaleManager.m_translateFilesPath}{lang}{Path.DirectorySeparatorChar}1_{Assembly.GetExecutingAssembly().GetName().Name}.txt", content);
                }
                content = ResourceLoader.LoadResourceString($"Commons.UI.i18n.{lang}.properties");
                if (content != null)
                {
                    File.WriteAllText($"{TLMLocaleManager.m_translateFilesPath}{lang}{Path.DirectorySeparatorChar}0_common_{DialogControl.VERSION}.txt", content);
                }

            }
            TLMLocaleManager.ReloadLanguage(true);
            DoWithSettingsUI(new UIHelperExtension(m_onSettingsUiComponent));
        }

        private void DoWithSettingsUI(UIHelperExtension helper)
        {
            foreach (Transform child in helper.Self?.transform)
            {
                GameObject.Destroy(child?.gameObject);
            }

            var newSprites = new List<SpriteInfo>();
            TextureAtlasUtils.LoadImagesFromResources("Commons.UI.Images", ref newSprites);
            TextureAtlasUtils.LoadImagesFromResources("UI.Images", ref newSprites);
            LogUtils.DoLog($"ADDING {newSprites.Count} sprites!");
            TextureAtlasUtils.RegenerateDefaultTextureAtlas(newSprites);

            TopSettingsUI(helper);

            if (UseGroup9)
            {
                CreateGroup9(helper);
            }

            LogUtils.DoLog("End Loading Options");
        }



        protected virtual void CreateGroup9(UIHelperExtension helper)
        {
            UIHelperExtension group9 = helper.AddGroupExtended(Locale.Get("BETAS_EXTRA_INFO"));
            Group9SettingsUI(group9);

            group9.AddCheckbox(Locale.Get("DEBUG_MODE"), DebugMode.value, delegate (bool val)
            { DebugMode.value = val; });
            group9.AddLabel(string.Format(Locale.Get("VERSION_SHOW"), FullVersion));
            group9.AddButton(Locale.Get("RELEASE_NOTES"), delegate ()
            {
                ShowVersionInfoPopup(true);
            });
            group9.AddButton("Report-a-bug helper", () => DialogControl.ShowModal(new DialogControl.BindProperties()
            {
                icon = IconName,
                title = "Report-a-bug helper",
                message = "If you find any problem with this mod, please send me the output_log.txt (or player.log on Mac/Linux) in the mod Workshop page. If applies, a printscreen can help too to make a better guess about what is happening wrong here...\n\n" +
                         "There's a link for a Workshop guide by <color #008800>aubergine18</color> explaining how to find your log file, depending of OS you're using.\nFeel free to create a topic at Workshop or just leave a comment linking your files.",
                showButton1 = true,
                textButton1 = "Okay...",
                showButton2 = true,
                textButton2 = "Go to the guide",
                showButton3 = true,
                textButton3 = "Go to mod page"
            }, (x) =>
            {
                if (x == 2)
                {
                    ColossalFramework.Utils.OpenUrlThreaded("https://steamcommunity.com/sharedfiles/filedetails/?id=463645931");
                    return false;
                }
                if (x == 3)
                {
                    ColossalFramework.Utils.OpenUrlThreaded("https://steamcommunity.com/sharedfiles/filedetails/?id=3007903394");
                    return false;
                }
                return true;
            }));

            if (!(GameObject.FindObjectOfType<TLMLocaleManager>() is null))
            {
                UIDropDown dd = null;
                dd = group9.AddDropdownLocalized("MOD_LANG", (new string[] { "GAME_DEFAULT_LANGUAGE" }.Concat(TLMLocaleManager.locales.Select(x => $"LANG_{x}")).Select(x => Locale.Get(x))).ToArray(), TLMLocaleManager.GetLoadedLanguage(), delegate (int idx)
                {
                    TLMLocaleManager.SaveLoadedLanguage(idx);
                    TLMLocaleManager.ReloadLanguage();
                    TLMLocaleManager.RedrawUIComponents();
                });
            }
            else
            {
                group9.AddLabel(string.Format(Locale.Get("LANG_CTRL_MOD_INFO"), Locale.Get("MOD_CONTROLLING_LOCALE")));
            }

        }

        public virtual void Group9SettingsUI(UIHelperExtension group9) { }

        protected virtual Tuple<string, string> GetButtonLink() => null;

        public bool ShowVersionInfoPopup(bool force = false)
        {
            if ((needShowPopup &&
                (SimulationManager.instance.m_metaData?.m_updateMode == SimulationManager.UpdateMode.LoadGame
                || SimulationManager.instance.m_metaData?.m_updateMode == SimulationManager.UpdateMode.NewGameFromMap
                || SimulationManager.instance.m_metaData?.m_updateMode == SimulationManager.UpdateMode.NewGameFromScenario
                || PackageManager.noWorkshop
                ))
                || force)
            {
                try
                {
                    string title = $"{SimpleName} v{Version}";
                    string notes = ResourceLoader.LoadResourceString("UI.VersionNotes.txt");
                    var fullWidth = notes.StartsWith("<extended>");
                    if (fullWidth)
                    {
                        notes = notes.Substring("<extended>".Length);
                    }
                    string text = $"{SimpleName} was updated! Release notes:\n\n{notes}\n\n<sprite _Button> Current Version: <color #FFFF00>{FullVersion}</color>";
                    var targetUrl = GetButtonLink();
                    ShowModal(new BindProperties()
                    {
                        icon = IconName,
                        showClose = true,
                        showButton1 = true,
                        textButton1 = "Okay!",
                        showButton2 = true,
                        textButton2 = "Workshop Page",
                        showButton3 = !(targetUrl is null),
                        textButton3 = targetUrl?.First ?? "",
                        messageAlign = UIHorizontalAlignment.Left,
                        useFullWindowWidth = fullWidth,
                        title = title,
                        message = text,
                    }, (x) =>
                    {
                        switch (x)
                        {
                            case 0:
                            case 1:
                                needShowPopup = false;
                                CurrentSaveVersion.value = FullVersion;
                                break;
                            case 2:
                                ColossalFramework.Utils.OpenUrlThreaded("https://steamcommunity.com/sharedfiles/filedetails/?id=3007903394");
                                break;
                            case 3:
                                if (targetUrl is not null)
                                {
                                    ColossalFramework.Utils.OpenUrlThreaded(targetUrl.Second);
                                }
                                break;
                        }
                        return x <= 1;
                    });

                    return true;
                }
                catch (Exception e)
                {
                    DoErrorLog("showVersionInfoPopup ERROR {0} {1}\n{2}", e.GetType(), e.Message, e.StackTrace);
                }
            }
            return false;
        }
        public void SearchIncompatibilitiesModal()
        {
            try
            {
                Dictionary<ulong, Tuple<string, string>> notes = SearchIncompatibilities();
                if (notes != null && notes.Count > 0)
                {
                    string title = $"{SimpleName} - Incompatibility report";
                    string text;
                    unchecked
                    {
                        text = $"Some conflicting mods were found active. Disable or unsubscribe them to make the <color yellow>{SimpleName}</color> work properly.\n\n" +
                           string.Join("\n\n", notes.Select(x => $"\t -{x.Value.First} (id: {(x.Key == (ulong)-1 ? "<LOCAL>" : x.Key.ToString())})\n" +
                            $"\t\t<color yellow>WHY?</color> {x.Value.Second ?? "This DLL have a name of an incompatible mod, but it's installed locally. Ignore this warning if you know what you are doing."}").ToArray()) +
                            $"\n\nDisable or unsubscribe them at main menu and try again!";
                    }
                    ShowModal(new BindProperties()
                    {
                        icon = IconName,
                        showButton1 = true,
                        textButton1 = "Err... Okay!",
                        messageAlign = UIHorizontalAlignment.Left,
                        title = title,
                        message = text,
                        useFullWindowWidth = true
                    }, (x) => true);
                }
            }
            catch (Exception e)
            {
                DoErrorLog("SearchIncompatibilitiesModal ERROR {0} {1}\n{2}", e.GetType(), e.Message, e.StackTrace);
            }
        }

        private void UnsubAuto()
        {
            if (AutomaticUnsubMods.Count > 0)
            {
                var modsToUnsub = PluginUtils.VerifyModsSubscribed(AutomaticUnsubMods);
                foreach (var mod in modsToUnsub)
                {
                    LogUtils.DoWarnLog($"Unsubscribing from mod: {mod.Value} (id: {mod.Key})");
                    PlatformService.workshop.Unsubscribe(new PublishedFileId(mod.Key));
                }
            }
        }

        public Dictionary<ulong, Tuple<string, string>> SearchIncompatibilities() => IncompatibleModList.Count == 0 ? null : PluginUtils.VerifyModsEnabled(IncompatibleModList, IncompatibleDllModList);
        public void OnViewStart() => ExtraOnViewStartActions();

        protected virtual void ExtraOnViewStartActions() { }

        protected virtual Dictionary<ulong, string> IncompatibleModList { get; } = new Dictionary<ulong, string>();
        protected virtual List<string> IncompatibleDllModList { get; } = new List<string>();

        private Dictionary<ulong, string> IncompatibleModListCommons { get; } = new Dictionary<ulong, string>();
        private List<string> IncompatibleDllModListCommons { get; } = new List<string>();
        protected virtual List<ulong> AutomaticUnsubMods { get; } = new List<ulong>();


        public IEnumerable<KeyValuePair<ulong, string>> IncompatibleModListAll => IncompatibleModListCommons.Union(IncompatibleModList);
        public IEnumerable<string> IncompatibleDllModListAll => IncompatibleDllModListCommons.Union(IncompatibleDllModList);

        public static SavedBool UseUuiIfAvailable { get; } = new SavedBool("_UseUuiIfAvailable", Settings.gameSettingsFile, true, true);
    }

}
