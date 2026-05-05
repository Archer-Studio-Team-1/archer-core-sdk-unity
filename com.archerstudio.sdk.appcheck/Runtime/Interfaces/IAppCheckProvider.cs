using System;

namespace ArcherStudio.SDK.AppCheck {

    public interface IAppCheckProvider {
        void Initialize(AppCheckConfig config, Action<bool> onComplete);
        void GetToken(Action<string> onToken);
        void Dispose();
    }
}
