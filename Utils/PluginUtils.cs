﻿using ColossalFramework;
using ColossalFramework.Plugins;
using ICities;
using Commons.Utils.UtilitiesClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Commons.Utils
{
    public static class PluginUtils
    {

        public static Dictionary<ulong, Tuple<string, string>> VerifyModsEnabled(Dictionary<ulong, string> modIds, List<string> modsDlls) =>
            Singleton<PluginManager>.instance.GetPluginsInfo().Where((PluginManager.PluginInfo pi) =>
            pi.assemblyCount > 0
            && pi.isEnabled
            && (
                (modIds?.Keys.Contains(pi.publishedFileID.AsUInt64) ?? false)
             || (modsDlls != null && pi.GetAssemblies().Where(x => modsDlls.Contains(x.GetName().Name)).Count() > 0)
            )
        ).ToDictionary(x => x.publishedFileID.AsUInt64, x => Tuple.New(((IUserMod)x.userModInstance).Name, x.publishedFileID.AsUInt64 != ~0UL && modIds.TryGetValue(x.publishedFileID.AsUInt64, out string message) ? message : null));
        public static Dictionary<ulong, string> VerifyModsSubscribed(List<ulong> modIds) => Singleton<PluginManager>.instance.GetPluginsInfo().Where((PluginManager.PluginInfo pi) =>
            pi.assemblyCount > 0
            && (modIds?.Contains(pi.publishedFileID.AsUInt64) ?? false)
        ).ToDictionary(x => x.publishedFileID.AsUInt64, x => ((IUserMod)x.userModInstance)?.Name);


        public static I GetImplementationTypeForMod<F, I>(GameObject objTarget, string dllName, string dllMinVersion, string nonFallbackClassName, string dllMaxVersion = null) where F : MonoBehaviour, I where I : Component
        {
            if (Singleton<PluginManager>.instance.GetPluginsInfo().Where((PluginManager.PluginInfo pi) =>
            pi.assemblyCount > 0
            && pi.isEnabled
            && (pi.GetAssemblies().Where(x => (x.GetName().Name == dllName) && x.GetName().Version.CompareTo(new Version(dllMinVersion)) >= 0 && (dllMaxVersion is null || x.GetName().Version.CompareTo(new Version(dllMaxVersion)) < 0)).Count() > 0)

        ).Count() > 0)
            {
                try
                {
                    var targetType = typeof(I).Assembly.GetType(nonFallbackClassName);
                    LogUtils.DoWarnLog($"Using {targetType?.Name} as implementation of {typeof(I).Name}");
                    return objTarget.AddComponent(targetType) as I;
                }
                catch (Exception e)
                {
                    LogUtils.DoWarnLog($"Failed loading integration with {dllName}. Fallback class was loaded. Check for a more recent version of that or this mod to fix this.\nDetails:\n{e}");
                    return objTarget.AddComponent<F>();
                }
            }
            else
            {
                LogUtils.DoWarnLog($"Using {typeof(F).Name}  (fallback) as implementation of {typeof(I).Name}");
                return objTarget.AddComponent<F>();
            }
        }
    }
}
