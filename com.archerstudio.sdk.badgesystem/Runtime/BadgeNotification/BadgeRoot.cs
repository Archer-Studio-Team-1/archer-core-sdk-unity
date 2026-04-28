using XNode;

namespace ArcherStudio.Badge.Runtime
{
    public class BadgeRoot : Node
    {
        [Output] public string child;

        private const string ROOT_KEY = "Root";

        public override object GetValue(NodePort port)
        {
            return ROOT_KEY;
        }
        
    }
}
