namespace Photura.Properties
{
    internal sealed partial class Settings :
        global::System.Configuration.ApplicationSettingsBase
    {
        private static Settings _defaultInstance =
            (Settings)global::System.Configuration.ApplicationSettingsBase
                .Synchronized(new Settings());

        public static Settings Default => _defaultInstance;

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1200")]
        public double WindowWidth
        {
            get => (double)this["WindowWidth"];
            set => this["WindowWidth"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("850")]
        public double WindowHeight
        {
            get => (double)this["WindowHeight"];
            set => this["WindowHeight"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("100")]
        public double WindowLeft
        {
            get => (double)this["WindowLeft"];
            set => this["WindowLeft"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("100")]
        public double WindowTop
        {
            get => (double)this["WindowTop"];
            set => this["WindowTop"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool WindowMaximized
        {
            get => (bool)this["WindowMaximized"];
            set => this["WindowMaximized"] = value;
        }
    }
}