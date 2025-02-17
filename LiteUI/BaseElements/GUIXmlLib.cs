﻿using ColossalFramework.Globalization;
using Commons.Interfaces;
using Commons.Libraries;
using Commons.Utils;
using System;
using System.Collections;
using UnityEngine;

namespace Commons.LiteUI.BaseElements
{
    public class GUIXmlLib<L, T> : GUIXmlLib<L, T, T> where T : class, ILibable where L : LibBaseFile<L, T>, new() { }
    public class GUIXmlLib<L, S, T> where T : S where S : class, ILibable where L : LibBaseFile<L, S>, new()
    {
        private string libraryFilter = "";
        private Vector2 libraryScroll;
        private Wrapper<string[]> librarySearchResults = new Wrapper<string[]>();
        private Coroutine librarySearchCoroutine;

        private Texture ImportTex = GUICommons.GetByNameFromDefaultAtlas("Import");
        private Texture ExportTex = GUICommons.GetByNameFromDefaultAtlas("Export");

        public string DeleteQuestionI18n { get; set; } = "";
        public string ImportI18n { get; set; } = "";
        public string ExportI18n { get; set; } = "";
        public string DeleteButtonI18n { get; set; } = "";
        public string NameAskingI18n { get; set; } = "";
        public string NameAskingOverwriteI18n { get; set; } = "";

        public FooterBarStatus Status { get; private set; }
        private void RestartLibraryFilterCoroutine()
        {
            if (librarySearchCoroutine != null)
            {
                CommonProperties.Controller.StopCoroutine(librarySearchCoroutine);
            }
            librarySearchCoroutine = CommonProperties.Controller.StartCoroutine(OnFilterLib());
        }
        private IEnumerator OnFilterLib()
        {
            yield return LibBaseFile<L, S>.Instance.BasicInputFiltering(libraryFilter, librarySearchResults);
        }

        public void DrawImportView(Rect areaRect, Action<T> OnSelect)
        {
            GUICommons.DoInHorizontal(() =>
            {
                var newFilterVal = GUILayout.TextField(libraryFilter);
                if (newFilterVal != libraryFilter)
                {
                    libraryFilter = newFilterVal;
                    RestartLibraryFilterCoroutine();
                }
            });
            GUICommons.DoInScroll(ref libraryScroll, () =>
            {
                var selectLayout = GUILayout.SelectionGrid(-1, librarySearchResults.Value, 1, GUILayout.Width(areaRect.width - 25));
                if (selectLayout >= 0)
                {
                    OnSelect(XmlUtils.TransformViaXml<S, T>(LibBaseFile<L, S>.Instance.Get(librarySearchResults.Value[selectLayout])));
                    Status = FooterBarStatus.Normal;
                }
            });
            if (GUILayout.Button(Locale.Get("CANCEL")))
            {
                Status = FooterBarStatus.Normal;
            }
        }

        public void Draw(Rect area, GUIStyle removeButtonStyle, Action doOnDelete, Func<T> getCurrent, Action<GUIStyle> onNormalDraw = null)
            => GUICommons.DoInArea(area,
                (x) => GUICommons.DoInHorizontal(() =>
                {
                    switch (Status)
                    {
                        case FooterBarStatus.AskingToRemove:
                            GUILayout.Label(string.Format(Locale.Get(DeleteQuestionI18n), getCurrent().SaveName));
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(Locale.Get("YES"), removeButtonStyle))
                            {
                                Status = FooterBarStatus.Normal;
                                doOnDelete();
                            }
                            if (GUILayout.Button(Locale.Get("NO")))
                            {
                                Status = FooterBarStatus.Normal;
                            }
                            break;
                        case FooterBarStatus.AskingToImport:
                            if (GUILayout.Button(Locale.Get("CANCEL")))
                            {
                                Status = FooterBarStatus.Normal;
                            }
                            break;
                        case FooterBarStatus.AskingToExport:
                            GUILayout.Label(Locale.Get(NameAskingI18n));
                            footerInputVal = GUILayout.TextField(footerInputVal, GUILayout.Width(150));
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(Locale.Get("SAVE")))
                            {
                                LibBaseFile<L, S>.Instance.EnsureFileExists();
                                if (LibBaseFile<L, S>.Instance.Get(footerInputVal) is null)
                                {
                                    LibBaseFile<L, S>.Instance.Add(footerInputVal, getCurrent());
                                    Status = FooterBarStatus.Normal;
                                }
                                else
                                {
                                    Status = FooterBarStatus.AskingToExportOverwrite;
                                }
                            }
                            if (GUILayout.Button(Locale.Get("CANCEL")))
                            {
                                Status = FooterBarStatus.Normal;
                            }
                            break;
                        case FooterBarStatus.AskingToExportOverwrite:
                            GUILayout.Label(string.Format(Locale.Get(NameAskingOverwriteI18n)));
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(Locale.Get("YES"), removeButtonStyle))
                            {
                                LibBaseFile<L, S>.Instance.Add(footerInputVal, getCurrent());
                                Status = FooterBarStatus.Normal;
                            }
                            if (GUILayout.Button(Locale.Get("NO")))
                            {
                                Status = FooterBarStatus.AskingToExport;
                            }
                            break;

                        case FooterBarStatus.Normal:
                            onNormalDraw?.Invoke(removeButtonStyle);
                            break;
                    }
                }));

        private FooterBarStatus m_currentHover;
        public void FooterDraw(GUIStyle removeButtonStyle)
        {
            var hoverSomething = false;
            GUI.SetNextControlName(GetHashCode() + "_IMPORT");
            if (GUILayout.Button(ImportTex, GUILayout.MaxHeight(20)))
            {
                GoToImport();
            }
            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                m_currentHover = FooterBarStatus.AskingToImport;
                hoverSomething = true;
            }
            GUI.SetNextControlName(GetHashCode() + "_EXPORT");
            if (GUILayout.Button(ExportTex, GUILayout.MaxHeight(20)))
            {
                GoToExport();
            }
            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                m_currentHover = FooterBarStatus.AskingToExport;
                hoverSomething = true;
            }
            if (!hoverSomething)
            {
                m_currentHover = default;
            }
            DrawLabel(() => DrawRemoveButton(removeButtonStyle));
        }

        public void DrawLabel(Action drawDefault)
        {
            switch (m_currentHover)
            {
                case FooterBarStatus.AskingToImport:
                    GUILayout.Label(Locale.Get(ImportI18n), GUILayout.Width(300), GUILayout.ExpandHeight(true));
                    break;
                case FooterBarStatus.AskingToExport:
                    GUILayout.Label(Locale.Get(ExportI18n), GUILayout.Width(300), GUILayout.ExpandHeight(true));
                    break;
                default:
                    GUILayout.Label("", GUILayout.Width(300), GUILayout.ExpandHeight(true));
                    break;
            }
            drawDefault?.Invoke();
            GUILayout.FlexibleSpace();
        }

        private void DrawRemoveButton(GUIStyle removeButtonStyle)
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Locale.Get(DeleteButtonI18n), removeButtonStyle))
            {
                GoToRemove();
            }
        }

        public void GoToRemove() => Status = FooterBarStatus.AskingToRemove;
        public void GoToExport()
        {
            Status = FooterBarStatus.AskingToExport;
            footerInputVal = "";
        }

        public void GoToImport()
        {
            Status = FooterBarStatus.AskingToImport;
            libraryFilter = "";
            librarySearchResults.Value = new string[0];
            RestartLibraryFilterCoroutine();
        }

        internal void ResetStatus() => Status = FooterBarStatus.Normal;

        private string footerInputVal = "";
    }
    public enum FooterBarStatus
    {
        Normal,
        AskingToRemove,
        AskingToExport,
        AskingToExportOverwrite,
        AskingToImport
    }
}
