using Cysharp.Text;
using UnityEngine;
using ArcherStudio.Trie;
using XNode;

namespace ArcherStudio.Badge.Runtime
{
    [System.Serializable]
    public partial class BadgeNode : XNode.Node
    {
        private const string FIELD = "parent";
        private const string DEFAULT_NAME = "BadgeNode";

        [Output] public string child;
        [Input] public string parent;

        public string key;
        public NodeType nodeType = NodeType.Multiple;

        [TextArea]
        public string description;

        #if UNITY_EDITOR
        private void OnValidate()
        {
            var p = GetInputValue<string>(FIELD);
            name = string.IsNullOrEmpty(key) ? DEFAULT_NAME : ZString.Concat(p, Const.SEPARATOR, key);
            //save this asset
            UnityEditor.EditorUtility.SetDirty(this);
            
            //verify the key is not empty, not contains the separator and not contains the parent value
            if (string.IsNullOrEmpty(key) || key.Contains(Const.SEPARATOR) || key.Contains(p))
            {
                Debug.LogError("Invalid key");
            }
        }
        #endif

        public override object GetValue(NodePort port)
        {
            var input = GetInputValue<string>(FIELD);
            var value = ZString.Concat(input, Const.SEPARATOR, key);
            return value;
        }
    }
}
