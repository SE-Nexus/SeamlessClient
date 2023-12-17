using System;
using Sandbox;
using Sandbox.Graphics.GUI;
using Shared.Plugin;
using VRage;
using VRage.Utils;
using VRageMath;

namespace SeamlessClient.GUI
{
    public class PluginConfigDialog : MyGuiScreenBase
    {
        private static readonly string Caption = $"{Seamless.Name} Configuration";
        public override string GetFriendlyName() => "PluginConfigDialog";

        private MyLayoutTable layoutTable;

        private MyGuiControlLabel gpsETAEnabledLabel;
        private MyGuiControlCheckbox gpsETAEnabledCheckbox;

        private MyGuiControlButton closeButton;

        public PluginConfigDialog() : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.5f, 0.7f), false, null, MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity)
        {
            EnabledBackgroundFade = true;
            m_closeOnEsc = true;
            m_drawEvenWithoutFocus = true;
            CanHideOthers = true;
            CanBeHidden = true;
            CloseButtonEnabled = true;
        }

        public override void LoadContent()
        {
            base.LoadContent();
            RecreateControls(true);
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            CreateControls();
            LayoutControls();
        }

        private void CreateControls()
        {
            AddCaption(Caption);

            var config = Common.Config;
            CreateCheckbox(out gpsETAEnabledLabel, out gpsETAEnabledCheckbox, config.GPSEtaEnabled, value => config.GPSEtaEnabled = value, "GPS ETA Enabled", "Enables the ETA display on GPS markers.");

            closeButton = new MyGuiControlButton(originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER, text: MyTexts.Get(MyCommonTexts.Ok), onButtonClick: OnOk);
        }

        private void OnOk(MyGuiControlButton _) => CloseScreen();

        private void CreateCheckbox(out MyGuiControlLabel labelControl, out MyGuiControlCheckbox checkboxControl, bool value, Action<bool> store, string label, string tooltip)
        {
            labelControl = new MyGuiControlLabel
            {
                Text = label,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP
            };

            checkboxControl = new MyGuiControlCheckbox(toolTip: tooltip)
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP,
                Enabled = true,
                IsChecked = value
            };
            checkboxControl.IsCheckedChanged += cb => store(cb.IsChecked);
        }

        private void LayoutControls()
        {
            var size = Size ?? Vector2.One;
            layoutTable = new MyLayoutTable(this, new Vector2(-0.3f * size.X, -0.375f * size.Y), new Vector2(0.6f * size.X, 0.8f * size.Y));
            layoutTable.SetColumnWidths(400f, 100f);
            layoutTable.SetRowHeights(80f, 50f, 50f, 50f, 50f, 220f, 50f);

            var row = 0;

            layoutTable.Add(gpsETAEnabledLabel, MyAlignH.Left, MyAlignV.Center, row, 0);
            layoutTable.Add(gpsETAEnabledCheckbox, MyAlignH.Left, MyAlignV.Center, row, 1);
            row++;

            layoutTable.Add(closeButton, MyAlignH.Center, MyAlignV.Center, row, 0, colSpan: 2);
        }
    }
}