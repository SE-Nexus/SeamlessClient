using Sandbox.AppCode;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Engine.Utils;
using Sandbox.Game.Gui;
using Sandbox.Game.Screens;
using Sandbox.Game.World;
using Sandbox.Game;
using Sandbox.Graphics.GUI;
using Sandbox.Graphics;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Audio;
using VRage.Input;
using VRage.Library.Utils;
using VRage.Utils;
using VRage;
using VRageMath;
using VRageRender;
using VRage.Collections;
using Sandbox.Game.Multiplayer;
using Sandbox.Engine.Platform;
using SeamlessClient.Components;
using System.Security.AccessControl;
using System.Reflection;
using System.Timers;

namespace SeamlessClient.GUI.Screens
{
    public class GUILoadingScreen : MyGuiScreenBase
    {
        public static readonly int STREAMING_TIMEOUT = 900;

        public static GUILoadingScreen Static;

        private MyGuiScreenBase m_screenToLoad;

        private readonly MyGuiScreenGamePlay m_screenToUnload;

        private string m_backgroundScreenTexture;

        private string m_backgroundTextureFromConstructor;

        private string m_customTextFromConstructor;

        private string m_rotatingWheelTexture;

        private object m_currentText;

        private MyGuiControlMultilineText m_multiTextControl;

        private StringBuilder m_authorWithDash;

        private MyGuiControlRotatingWheel m_wheel;

        private bool m_exceptionDuringLoad;

        public static string LastBackgroundTexture;

        public Action OnLoadingXMLAllowed;

        public static int m_currentTextIdx = 0;

        private volatile bool m_loadInDrawFinished;

        private bool m_loadFinished;

        private bool m_runLoadEnded;

        private bool m_isStreamed;

        private int m_streamingTimeout;

        private float m_progress;

        private int m_displayProgress;

        private float m_localProgress;

        private float m_localTotal;

        private float m_localProgressDelta;

        private float m_localProgressMin;

        private string m_font = "LoadingScreen";

        private bool m_closed;

        private MyTimeSpan m_loadingTimeStart;

        private static Dictionary<LoadingProgress, float> _progressMap;

        private Timer backgroundTimer = new Timer(8000);




        /// <summary>
        /// Event created once the screen has been loaded and added to GUI manager.
        /// </summary>
        public event Action OnScreenLoadingFinished;

        public GUILoadingScreen(MyGuiScreenBase screenToLoad, MyGuiScreenGamePlay screenToUnload, string textureFromConstructor, string customText = null)
            : base(Vector2.Zero)
        {
            base.CanBeHidden = false;
            m_isTopMostScreen = true;

            object instance = PatchUtils.GetProperty(PatchUtils.MyLoadingPerformance, "Instance").GetValue(null);
            if(instance != null)
                PatchUtils.GetMethod(PatchUtils.MyLoadingPerformance, "StartTiming").Invoke(instance, null);

            //MyLoadingPerformance.Instance.StartTiming();
            Static = this;
            m_screenToLoad = screenToLoad;
            m_screenToUnload = screenToUnload;
            m_closeOnEsc = false;
            base.DrawMouseCursor = false;
            m_loadInDrawFinished = false;
            m_drawEvenWithoutFocus = true;

            MethodInfo text = PatchUtils.GetMethod(PatchUtils.MyLoadingScreenText, "GetRandomText");
            m_currentText = text.Invoke(null, null);
            //m_currentText = MyLoadingScreenText.GetRandomText();


            m_isFirstForUnload = true;
            MyGuiSandbox.SetMouseCursorVisibility(false);
            m_rotatingWheelTexture = "Textures\\GUI\\screens\\screen_loading_wheel_loading_screen.dds";
            m_backgroundTextureFromConstructor = textureFromConstructor;
            m_customTextFromConstructor = customText;
            m_loadFinished = false;
            if (m_screenToLoad != null)
            {
                MySandboxGame.IsUpdateReady = false;
                MySandboxGame.AreClipmapsReady = !Sync.IsServer || Game.IsDedicated || MyExternalAppBase.Static != null;
                MySandboxGame.RenderTasksFinished = Game.IsDedicated || MyExternalAppBase.Static != null;
            }
            m_authorWithDash = new StringBuilder();
            RecreateControls(true);
            MyInput.Static.EnableInput(false);
            if (Sync.IsServer || Game.IsDedicated || MyMultiplayer.Static == null)
            {
                m_isStreamed = true;
            }
            else
            {
                MyMultiplayer.Static.LocalRespawnRequested += OnLocalRespawnRequested;
            }
            MySession.LoadingStep = (Action<LoadingProgress>)Delegate.Combine(MySession.LoadingStep, new Action<LoadingProgress>(SetProgress));
            MySession.LoadingLocalAdd = (Action<float>)Delegate.Combine(MySession.LoadingLocalAdd, new Action<float>(AddLocalProgress));
            MySession.LoadingLocalClear = (Action)Delegate.Combine(MySession.LoadingLocalClear, new Action(ClearLocalProgress));
            MySession.LoadingLocalBoundSet = (Action<LoadingProgress, LoadingProgress>)Delegate.Combine(MySession.LoadingLocalBoundSet, new Action<LoadingProgress, LoadingProgress>(SetLocalBounds));
            MySession.LoadingLocalTotalSet = (Action<float>)Delegate.Combine(MySession.LoadingLocalTotalSet, new Action<float>(SetLocalTotal));


           




        }

        private void OnLocalRespawnRequested()
        {
            (MyMultiplayer.Static as MyMultiplayerClientBase).RequestBatchConfirmation();
            MyMultiplayer.Static.PendingReplicablesDone += MyMultiplayer_PendingReplicablesDone;
            MyMultiplayer.Static.LocalRespawnRequested -= OnLocalRespawnRequested;
            m_streamingTimeout = 0;
        }

        private void MyMultiplayer_PendingReplicablesDone()
        {
            m_isStreamed = true;
            if (MySession.Static.VoxelMaps.Instances.Count > 0)
            {
                MySandboxGame.AreClipmapsReady = false;
            }
            MyMultiplayer.Static.PendingReplicablesDone -= MyMultiplayer_PendingReplicablesDone;
        }

        public GUILoadingScreen(MyGuiScreenBase screenToLoad, MyGuiScreenGamePlay screenToUnload)
            : this(screenToLoad, screenToUnload, null)
        {
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            Vector2 vector = MyGuiManager.MeasureString(m_font, MyTexts.Get(MyCommonTexts.LoadingPleaseWaitUppercase), 1.1f);
            m_wheel = new MyGuiControlRotatingWheel(MyGuiConstants.LOADING_PLEASE_WAIT_POSITION - new Vector2(0f, 0.09f + vector.Y), MyGuiConstants.ROTATING_WHEEL_COLOR, 0.36f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, m_rotatingWheelTexture, false, MyPerGameSettings.GUI.MultipleSpinningWheels);
            m_multiTextControl = new MyGuiControlMultilineText(contents: string.IsNullOrEmpty(m_customTextFromConstructor) ? new StringBuilder(m_currentText.ToString()) : new StringBuilder(m_customTextFromConstructor), position: new Vector2(0.5f, 0.66f), size: new Vector2(0.9f, 0.2f), backgroundColor: Vector4.One, font: m_font, textScale: 1f, textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM, drawScrollbarV: false, drawScrollbarH: false);
            m_multiTextControl.BorderEnabled = false;
            m_multiTextControl.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM;
            m_multiTextControl.TextBoxAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM;
            Controls.Add(m_wheel);
            
            
            RefreshText();
        }

        public override string GetFriendlyName()
        {
            return "GUILoadingScreen";
        }

        public override void LoadContent()
        {
            m_loadingTimeStart = new MyTimeSpan(Stopwatch.GetTimestamp());
            MySandboxGame.Log.WriteLine("MyGuiScreenLoading.LoadContent - START");
            MySandboxGame.Log.IncreaseIndent();
            m_backgroundScreenTexture = m_backgroundTextureFromConstructor ?? GetRandomBackgroundTexture();
            if (m_screenToUnload != null)
            {
                m_screenToUnload.IsLoaded = false;
                m_screenToUnload.CloseScreenNow();
            }
            base.LoadContent();
            MyRenderProxy.LimitMaxQueueSize = true;
            if (m_screenToLoad != null && !m_loadInDrawFinished && m_loadFinished)
            {
                m_screenToLoad.State = MyGuiScreenState.OPENING;
                m_screenToLoad.LoadContent();
            }
            else
            {
                m_loadFinished = false;
            }
            MySandboxGame.Log.DecreaseIndent();
            MySandboxGame.Log.WriteLine("MyGuiScreenLoading.LoadContent - END");

            
        }

        public static string GetRandomBackgroundTexture()
        {
            

            string text = MyUtils.GetRandomInt(MyPerGameSettings.GUI.LoadingScreenIndexRange.X, MyPerGameSettings.GUI.LoadingScreenIndexRange.Y + 1).ToString().PadLeft(3, '0');
            return "Textures\\GUI\\Screens\\loading_background_" + text + ".dds";
        }

        public override void UnloadContent()
        {
            if (m_backgroundScreenTexture != null)
            {
                MyRenderProxy.UnloadTexture(m_backgroundScreenTexture);
            }
            if (m_backgroundTextureFromConstructor != null)
            {
                MyRenderProxy.UnloadTexture(m_backgroundTextureFromConstructor);
            }
            if (m_backgroundScreenTexture != null)
            {
                MyRenderProxy.UnloadTexture(m_rotatingWheelTexture);
            }
            if (m_screenToLoad != null && !m_loadFinished && m_loadInDrawFinished)
            {
                m_screenToLoad.UnloadContent();
                m_screenToLoad.UnloadData();
                m_screenToLoad = null;
            }
            if (m_screenToLoad != null && !m_loadInDrawFinished)
            {
                m_screenToLoad.UnloadContent();
            }
            MyRenderProxy.LimitMaxQueueSize = false;
            base.UnloadContent();
            Static = null;
        }

        public override bool Update(bool hasFocus)
        {
            if (!base.Update(hasFocus))
            {
                return false;
            }
            if (base.State == MyGuiScreenState.OPENED && !m_loadFinished)
            {
                m_loadFinished = true;
                MyHud.ScreenEffects.FadeScreen(0f);
                MyAudio.Static.Mute = true;
                MyAudio.Static.StopMusic();
                MyAudio.Static.ChangeGlobalVolume(0f, 0f);
                MyRenderProxy.DeferStateChanges(true);
                DrawLoading();
                if (m_screenToLoad != null)
                {
                    MySandboxGame.Log.WriteLine("RunLoadingAction - START");

                    try
                    {

                        RunLoad();

                    }catch(Exception ex)
                    {
                        MySandboxGame.Log.WriteLine($"RunLoadingAction ERROR: {ex}");
                    }
                    Action<LoadingProgress> loadingStep = MySession.LoadingStep;
                    if (loadingStep != null)
                    {
                        loadingStep(LoadingProgress.PROGRESS_STEP10);
                    }
                    MySandboxGame.Log.WriteLine("RunLoadingAction - END");
                }
                if (m_screenToLoad != null)
                {
                    MyScreenManager.AddScreenNow(m_screenToLoad);
                    m_screenToLoad.Update(false);
                }
                m_screenToLoad = null;
                m_wheel.ManualRotationUpdate = true;
            }
            m_streamingTimeout++;
            bool flag = Sync.IsServer || Game.IsDedicated || MyMultiplayer.Static == null || !MyFakes.ENABLE_WAIT_UNTIL_MULTIPLAYER_READY || m_isStreamed || (MyFakes.LOADING_STREAMING_TIMEOUT_ENABLED && m_streamingTimeout >= STREAMING_TIMEOUT);
            if (m_runLoadEnded && m_loadFinished && ((MySandboxGame.IsGameReady && flag && MySandboxGame.AreClipmapsReady) || m_exceptionDuringLoad) && !m_closed)
            {
                MyRenderProxy.DeferStateChanges(false);
                MyHud.ScreenEffects.FadeScreen(1f, (!MyFakes.TESTING_TOOL_PLUGIN) ? 5f : 0f);
                if (MyHud.ScreenEffects.IsBlackscreenFadeInProgress())
                {
                    MyHudScreenEffects screenEffects = MyHud.ScreenEffects;
                    screenEffects.OnBlackscreenFadeFinishedCallback = (Action)Delegate.Combine(screenEffects.OnBlackscreenFadeFinishedCallback, new Action(MyGameService.OnLoadingScreenCompleted));
                }
                else
                {
                    MyGameService.OnLoadingScreenCompleted();
                }
                if (!m_exceptionDuringLoad && this.OnScreenLoadingFinished != null)
                {
                    this.OnScreenLoadingFinished();
                    this.OnScreenLoadingFinished = null;
                }
                CloseScreenNow();
                DrawLoading();
                MyTimeSpan myTimeSpan = new MyTimeSpan(Stopwatch.GetTimestamp()) - m_loadingTimeStart;
                MySandboxGame.Log.WriteLine("Loading duration: " + myTimeSpan.Seconds);
                m_closed = true;
            }
            else if (m_loadFinished && !MySandboxGame.AreClipmapsReady && MySession.Static != null && MySession.Static.VoxelMaps.Instances.Count == 0)
            {
                MySandboxGame.AreClipmapsReady = true;
            }
            return true;
        }

        private void RunLoad()
        {
            m_exceptionDuringLoad = false;
            try
            {
                m_screenToLoad.RunLoadingAction();
            }
            catch (MyLoadingNeedXMLException ex)
            {
                m_exceptionDuringLoad = true;
                if (OnLoadingXMLAllowed != null)
                {
                    UnloadOnException(false);
                    if (MySandboxGame.Static.SuppressLoadingDialogs)
                    {
                        OnLoadingXMLAllowed();
                        return;
                    }
                    MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Error, MyMessageBoxButtonsType.OK, MyTexts.Get(MyCommonTexts.LoadingNeedsXML), MyTexts.Get(MyCommonTexts.MessageBoxCaptionInfo), null, null, null, null, delegate
                    {
                        OnLoadingXMLAllowed();
                    }));
                }
                else
                {
                    OnLoadException(ex, new StringBuilder(ex.Message), 1.5f);
                }
            }
            catch (MyLoadingException ex2)
            {
                OnLoadException(ex2, new StringBuilder(ex2.Message), 1.5f);
                m_exceptionDuringLoad = true;
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception e)
            {
                OnLoadException(e, MyTexts.Get(MyCommonTexts.WorldFileIsCorruptedAndCouldNotBeLoaded));
                m_exceptionDuringLoad = true;
            }
            finally
            {
                m_runLoadEnded = true;
            }
        }

        protected override void OnClosed()
        {
            MyRenderProxy.DeferStateChanges(false);
            base.OnClosed();
            MyInput.Static.EnableInput(true);
            MyAudio.Static.Mute = false;
            MySession.LoadingStep = (Action<LoadingProgress>)Delegate.Remove(MySession.LoadingStep, new Action<LoadingProgress>(SetProgress));
            MySession.LoadingLocalAdd = (Action<float>)Delegate.Remove(MySession.LoadingLocalAdd, new Action<float>(AddLocalProgress));
            MySession.LoadingLocalClear = (Action)Delegate.Remove(MySession.LoadingLocalClear, new Action(ClearLocalProgress));
            MySession.LoadingLocalBoundSet = (Action<LoadingProgress, LoadingProgress>)Delegate.Remove(MySession.LoadingLocalBoundSet, new Action<LoadingProgress, LoadingProgress>(SetLocalBounds));
            MySession.LoadingLocalTotalSet = (Action<float>)Delegate.Remove(MySession.LoadingLocalTotalSet, new Action<float>(SetLocalTotal));
        }

        private void UnloadOnException(bool exitToMainMenu)
        {
            MyRenderProxy.DeferStateChanges(false);
            DrawLoading();
            m_screenToLoad = null;
            if (MyGuiScreenGamePlay.Static != null)
            {
                MyGuiScreenGamePlay.Static.UnloadData();
                MyGuiScreenGamePlay.Static.UnloadContent();
            }
            MySandboxGame.IsUpdateReady = true;
            MySandboxGame.AreClipmapsReady = true;
            MySandboxGame.RenderTasksFinished = true;
            if (exitToMainMenu)
            {
                MySessionLoader.UnloadAndExitToMenu();
            }
            else
            {
                MySessionLoader.Unload();
            }
        }

        private void OnLoadException(Exception e, StringBuilder errorText, float heightMultiplier = 1f)
        {
            MySandboxGame.Log.WriteLine("ERROR: Loading screen failed");
            MySandboxGame.Log.WriteLine(e);
            UnloadOnException(true);
            MyLoadingNeedDLCException exception;
            MyGuiScreenMessageBox myGuiScreenMessageBox;
            if ((exception = e as MyLoadingNeedDLCException) != null)
            {
                myGuiScreenMessageBox = MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Info, MyMessageBoxButtonsType.YES_NO, messageCaption: MyTexts.Get(MyCommonTexts.RequiresAnyDlc), messageText: new StringBuilder().AppendFormat(MyTexts.GetString(MyPlatformGameSettings.LocalizationKeys.ScenarioRequiresDlc), MyTexts.GetString(exception.RequiredDLC.DisplayName)), okButtonText: null, cancelButtonText: null, yesButtonText: null, noButtonText: null, callback: delegate (MyGuiScreenMessageBox.ResultEnum result)
                {
                    if (result == MyGuiScreenMessageBox.ResultEnum.YES)
                    {
                        MyGameService.OpenDlcInShop(exception.RequiredDLC.AppId);
                    }
                });
            }
            else
            {
                myGuiScreenMessageBox = MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Error, MyMessageBoxButtonsType.OK, errorText, MyTexts.Get(MyCommonTexts.MessageBoxCaptionError));
                Vector2 value = myGuiScreenMessageBox.Size.Value;
                value.Y *= heightMultiplier;
                myGuiScreenMessageBox.Size = value;
                myGuiScreenMessageBox.RecreateControls(false);
            }
            MyGuiSandbox.AddScreen(myGuiScreenMessageBox);
        }

        public override void HandleInput(bool receivedFocusInThisUpdate)
        {
            base.HandleInput(receivedFocusInThisUpdate);
        }

        public void SetLocalBounds(LoadingProgress start, LoadingProgress end)
        {
            m_localProgressDelta = _progressMap[end] - _progressMap[start];
            m_localProgressMin = m_progress;
        }

        public void ClearLocalProgress()
        {
            m_localTotal = 1f;
            m_localProgress = 0f;
            m_localProgressDelta = 0f;
        }

        public void SetLocalTotal(float total)
        {
            m_localTotal = total;
            m_localProgress = 0f;
        }

        public void AddLocalProgress(float amount = 1f)
        {
            SetLocalProgress(m_localProgress + amount);
        }

        public void SetLocalProgress(float progress)
        {
            m_localProgress = progress;
            if (m_localTotal > 0f)
            {
                float num = MathHelper.Clamp(m_localProgress / m_localTotal, 0f, 1f);
                SetProgress(m_localProgressMin + num * m_localProgressDelta);
            }
        }

        public void SetProgress(LoadingProgress progress)
        {
            SetProgress(_progressMap[progress]);
        }

        private void SetProgress(float progress)
        {
            m_progress = progress;
            int num = (int)(m_progress * 100f);
            if (num != m_displayProgress)
            {
                m_displayProgress = num;
                if (MyUtils.MainThread == System.Threading.Thread.CurrentThread)
                {
                    DrawLoading();
                }
            }
        }

        public bool DrawLoading()
        {
            DrawInternal();
            bool result = base.Draw();
            MyRenderProxy.AfterUpdate(null, false);
            MyRenderProxy.BeforeUpdate();
            return result;
        }

        private void DrawInternal()
        {
            Color color = new Color(255, 255, 255, 250);
            color.A = (byte)((float)(int)color.A * m_transitionAlpha);
            Rectangle fullscreenRectangle = MyGuiManager.GetFullscreenRectangle();
            MyGuiManager.DrawSpriteBatch("Textures\\GUI\\Blank.dds", fullscreenRectangle, Color.Black, false, true);
            Rectangle outRect;
            MyGuiManager.GetSafeHeightFullScreenPictureSize(MyGuiConstants.LOADING_BACKGROUND_TEXTURE_REAL_SIZE, out outRect);

            bool isCustom = false;
            if(LoadingScreenComponent.CustomLoadingTextures.Count != 0)
            {
                if(LoadingScreenComponent.CustomLoadingTextures.Count > 1)
                {
                    if (!backgroundTimer.Enabled)
                    {
                        backgroundTimer.Elapsed += BackgroundTimer_Elapsed;
                        backgroundTimer.Start();
                        m_backgroundScreenTexture = LoadingScreenComponent.getRandomLoadingScreen();
                    }

                }
                else
                {
                    m_backgroundScreenTexture = LoadingScreenComponent.CustomLoadingTextures.First();
                }

                isCustom = true;
            }


            MyGuiManager.DrawSpriteBatch(m_backgroundScreenTexture, outRect, new Color(new Vector4(1f, 1f, 1f, m_transitionAlpha)), true, true);
            MyGuiManager.DrawSpriteBatch("Textures\\Gui\\Screens\\screen_background_fade.dds", outRect, new Color(new Vector4(1f, 1f, 1f, m_transitionAlpha)), true, true);
            //MyGuiSandbox.DrawGameLogoHandler(m_transitionAlpha, MyGuiManager.ComputeFullscreenGuiCoordinate(MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, 44, 68), new Vector2(0.005f, 0.19f));
            LastBackgroundTexture = m_backgroundScreenTexture;

            string loading = MyTexts.GetString(MyCommonTexts.LoadingPleaseWaitUppercase);
            //If our server name is not empty:
            if (!string.IsNullOrEmpty(LoadingScreenComponent.JoiningServerName))
            {
                loading = $"Loading into {LoadingScreenComponent.JoiningServerName}...";
            }


            MyGuiManager.DrawString(m_font, loading, MyGuiConstants.LOADING_PLEASE_WAIT_POSITION, MyGuiSandbox.GetDefaultTextScaleWithLanguage() * 1.1f, new Color(MyGuiConstants.LOADING_PLEASE_WAIT_COLOR * m_transitionAlpha), MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM);
            MyGuiManager.DrawString(m_font, string.Format("{0}%", m_displayProgress), MyGuiConstants.LOADING_PERCENTAGE_POSITION, MyGuiSandbox.GetDefaultTextScaleWithLanguage() * 1.1f, new Color(MyGuiConstants.LOADING_PLEASE_WAIT_COLOR * m_transitionAlpha), MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM);
            
            
            
            if (!isCustom && string.IsNullOrEmpty(m_customTextFromConstructor))
            {
                string font = m_font;
                Vector2 positionAbsoluteBottomLeft = m_multiTextControl.GetPositionAbsoluteBottomLeft();
                Vector2 textSize = m_multiTextControl.TextSize;
                MyGuiManager.DrawString(normalizedCoord: positionAbsoluteBottomLeft + new Vector2((m_multiTextControl.Size.X - textSize.X) * 0.5f + 0.025f, 0.025f), font: font, text: m_authorWithDash.ToString(), scale: MyGuiSandbox.GetDefaultTextScaleWithLanguage());
                m_multiTextControl.Draw(1f, 1f);
            }

            /* Custom draws */

            MyGuiManager.DrawString(m_font, "Nexus & SeamlessClient Made by: Casimir", new Vector2(0.95f, 0.95f),
               MyGuiSandbox.GetDefaultTextScaleWithLanguage() * 1.1f,
               new Color(MyGuiConstants.LOADING_PLEASE_WAIT_COLOR * m_transitionAlpha),
               MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM);



        }

        

        private void BackgroundTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_backgroundScreenTexture = LoadingScreenComponent.getRandomLoadingScreen();
        }




        public override bool Draw()
        {
            DrawInternal();
            return base.Draw();
        }

        private void RefreshText()
        {
            if (string.IsNullOrEmpty(m_customTextFromConstructor))
            {
                m_multiTextControl.TextEnum = MyStringId.GetOrCompute(m_currentText.ToString());
                if (m_currentText.GetType() == PatchUtils.MyLoadingScreenQuote)
                {
                    MyStringId author = (MyStringId)PatchUtils.GetField(PatchUtils.MyLoadingScreenQuote, "Author").GetValue(m_currentText);
                    m_authorWithDash.Clear().Append("- ").AppendStringBuilder(MyTexts.Get(author))
                        .Append(" -");
                }
            }
        }

        public override void OnRemoved()
        {
            base.OnRemoved();
        }

        static GUILoadingScreen()
        {
            Dictionary<LoadingProgress, float> dictionary = new Dictionary<LoadingProgress, float>();
            dictionary[LoadingProgress.PROGRESS_STEP1] = 0.01f;
            dictionary[LoadingProgress.PROGRESS_STEP2] = 0.05f;
            dictionary[LoadingProgress.PROGRESS_STEP2_1_BEFORE_COMPILE_SCRIPTS] = 0.05f;
            dictionary[LoadingProgress.PROGRESS_STEP2_2_AFTER_COMPILE_SCRIPTS] = 0.2f;
            dictionary[LoadingProgress.PROGRESS_STEP2_3_LOAD_DEFINITIONS] = 0.26f;
            dictionary[LoadingProgress.PROGRESS_STEP2_4_POST_LOAD_DEFINITIONS] = 0.26f;
            dictionary[LoadingProgress.PROGRESS_STEP2_5_BEFORE_LOAD_VOXELS] = 0.3f;
            dictionary[LoadingProgress.PROGRESS_STEP2_6_AFTER_LOAD_VOXELS] = 0.4f;
            dictionary[LoadingProgress.PROGRESS_STEP2_6_AFTER_LOAD_GAME_DEFINITION] = 0.44f;
            dictionary[LoadingProgress.PROGRESS_STEP3] = 0.45f;
            dictionary[LoadingProgress.PROGRESS_STEP4] = 0.5f;
            dictionary[LoadingProgress.PROGRESS_STEP5] = 0.9f;
            dictionary[LoadingProgress.PROGRESS_STEP6] = 0.91f;
            dictionary[LoadingProgress.PROGRESS_STEP7] = 0.92f;
            dictionary[LoadingProgress.PROGRESS_STEP8] = 0.93f;
            dictionary[LoadingProgress.PROGRESS_STEP9] = 0.99f;
            dictionary[LoadingProgress.PROGRESS_STEP10] = 1f;
            _progressMap = dictionary;
        }

    }
}
