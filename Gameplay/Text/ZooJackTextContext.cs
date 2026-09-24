using System;
using UnityEngine;
using UnityEngine.Localization.Metadata;

namespace ZooJack
{
    /// <summary>
    /// ZooJack 문구가 어디에서, 언제, 어떤 값과 함께 보이는지 기록한다.
    /// String Table의 언어별 값이 아니라 Shared Entry에 붙으므로 언어가 늘어도 맥락은 한 번만 관리한다.
    /// </summary>
    [Metadata(
        MenuItem = "ZooJack/문구 상황",
        AllowMultiple = false,
        AllowedTypes = MetadataType.SharedStringTableEntry)]
    [Serializable]
    public sealed class ZooJackTextContext : IMetadata
    {
        [SerializeField] private string category;
        [SerializeField] private string scene;
        [SerializeField] private string target;
        [SerializeField, TextArea(1, 4)] private string trigger;
        [SerializeField] private string variables;
        [SerializeField, TextArea(1, 4)] private string preview;
        [SerializeField, TextArea(1, 5)] private string notes;
        [SerializeField, Min(0)] private int maxLines;

        public string Category { get => category; set => category = value; }
        public string Scene { get => scene; set => scene = value; }
        public string Target { get => target; set => target = value; }
        public string Trigger { get => trigger; set => trigger = value; }
        public string Variables { get => variables; set => variables = value; }
        public string Preview { get => preview; set => preview = value; }
        public string Notes { get => notes; set => notes = value; }
        public int MaxLines { get => maxLines; set => maxLines = Mathf.Max(0, value); }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return Contains(category, query) || Contains(scene, query) || Contains(target, query)
                || Contains(trigger, query) || Contains(variables, query) || Contains(preview, query)
                || Contains(notes, query);
        }

        private static bool Contains(string value, string query) =>
            !string.IsNullOrEmpty(value)
            && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
