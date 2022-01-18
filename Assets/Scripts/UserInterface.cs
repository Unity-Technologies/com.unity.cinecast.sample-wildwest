using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UserInterface : MonoBehaviour
{
    [Header("Authorization")]
    public GameObject authorizationPanel;
    public TMP_InputField jwtInput;
    public Button startCinecastButton;

    [Header("Session Management")]
    public GameObject sessionManagementPanel;
    public Button showPreviousSessionsButton;
    public GameObject sessionButtonPrefab;
    public RectTransform sessionScrollRect;

    [Header("New Session:")]
    public Button startNewSessionButton;
    public GameObject newSessionSettingsPanel;
    public Toggle canSendSpecatorEvents;
    public Toggle calculateInterest;
    public TMP_InputField sessionNameInput;
    public TMP_InputField sessionPasswordInput;
    public Button startButton;

    [Header("Password Panel")]
    public Sprite lockedSprite;
    public Sprite unlockedSprite;
    public GameObject passwordPanel;
    public TMP_InputField passwordInput;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Recording Panel")]
    public GameObject recordingPanel;
    public TMP_Dropdown cameraViewDropdown;
    public TextMeshProUGUI watcherText;
    public TextMeshProUGUI currentStatsText;
    public Button stopRecordingButton;
    public Button interveneButton;
    public TextMeshProUGUI spectatorItemDropTextRecording;

    [Header("Playback Panel")]
    public TextMeshProUGUI spectatorText;
    public TextMeshProUGUI hidersRemainingText;
    public Button backToMainMenuButton;
    public GameObject playbackPanel;
    public Button playPauseButton;
    public Sprite playSprite;
    public Sprite pauseSprite;
    public Sprite stopSprite;
    public Slider timelineSlider;
    public TextMeshProUGUI timeText;
    public GameObject sessionEndedPanel;
    public Button backToMainButton;
    public Button stayInPlaybackModeButton;
    public TextMeshProUGUI spectatorItemDropTextPlayback;
    public float itemDropNotificationTime;
    public Sprite invincibleSprite;
    public Sprite magicRakeSprite;

    [Header("POI Panel")] 
    public Gradient poiGradient;
    public GameObject poiItemPrefab;
    public GameObject poiPanel;

    [Header("Spectator Events")]
    public GameObject spectatorEventPrefab;
    public GameObject specatorEventPanel;

    private bool notificationShow;
    private float notificationTimer;

    private static UserInterface m_Instance;
    private SessionManagementState sessionManagementState;

#region Properties
    public static UserInterface Instance { get{ return m_Instance;}}
#endregion

    private enum SessionManagementState
    {
        PreviousSessions,
        NewSession
    }

    private void Awake()
    {
        if(m_Instance == null){
            m_Instance = this;
            GameObject.DontDestroyOnLoad(this.gameObject);
        } else {
            Destroy(gameObject);
        }

        authorizationPanel.SetActive(false);
        sessionManagementPanel.SetActive(false);
        passwordPanel.SetActive(false);
        recordingPanel.SetActive(false);


        if(!CinecastManager.Instance.HasPersonalKey() || !CinecastManager.Instance.IgnoreAuthorization){
            SetupAuthorizationPanel();
        } else {
            CinecastManager.Instance.StartupCinecast();
            SetupSessionManagementPanel();
            CinecastManager.Instance.GetSessions();
        }

        DemoManager.Instance.onDemoStateChanged += OnDemoStateChanged;
        CameraManager.Instance.ChangeCamera("MainMenu");
    }

    private void OnDemoStateChanged(DemoState state)
    {
        switch(state)
        {
            case DemoState.MainMenu:
                CameraManager.Instance.ChangeCamera("MainMenu");
                sessionManagementState = SessionManagementState.PreviousSessions;
                newSessionSettingsPanel.SetActive(false);
                CinecastManager.Instance.StartupCinecast();
                DemoManager.Instance.SetupMainMenu();
                SetupSessionManagementPanel();
                CinecastManager.Instance.GetSessions();
            break;

            case DemoState.Recording:
                CameraManager.Instance.ChangeCamera("WorldView");
                DemoManager.Instance.SetupRecording();
                SetupRecordingUI();
            break;

            case DemoState.Playback:
                DemoManager.Instance.SetupPlayback();
                SetupPlaybackUI();
                SetupPOIPanel();
                SetupSpecatorEventsPanel();
            break;
        }
    }

    private void Update()
    {
        switch(DemoManager.Instance.demoState)
        {
            case DemoState.Recording:
                UpdateRecordingUI();
                break;

            case DemoState.Playback:
                UpdatePlaybackUI();
                UpdatePOIButtons();
                break;
        }

        if(notificationShow == true)
        {
            if(Time.time > notificationTimer + itemDropNotificationTime)
            {
                spectatorItemDropTextPlayback.gameObject.SetActive(false);
                spectatorItemDropTextRecording.gameObject.SetActive(false);
                notificationShow = false;
            }
        }
    }

    private void SetupAuthorizationPanel()
    {
        authorizationPanel.SetActive(true);

        startCinecastButton.onClick.RemoveAllListeners();
        startCinecastButton.onClick.AddListener(() => {
            if(string.IsNullOrEmpty(jwtInput.text) && !CinecastManager.Instance.HasPersonalKey())
            {
                Debug.LogError("Couldn't authenticate Cinecast -> No JWT entered and no personal & secret key set in CinecastManager Inspector!");
            }
            else
            {
                CinecastManager.Instance.JWT = jwtInput.text;
                CinecastManager.Instance.StartupCinecast();
                authorizationPanel.SetActive(false);
                SetupSessionManagementPanel();
            }
        });
    }

    public void SetupSessionManagementPanel()
    {
        sessionManagementPanel.SetActive(true);

        if(sessionManagementState == SessionManagementState.PreviousSessions)
        {
            showPreviousSessionsButton.onClick.RemoveAllListeners();
            showPreviousSessionsButton.enabled = false;

            startNewSessionButton.onClick.RemoveAllListeners();
            startNewSessionButton.enabled = true;
            startNewSessionButton.onClick.AddListener(() =>
            {
                SetupNewSessionPanel();
            });

            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(() =>
            {
                if(CinecastManager.Instance.SelectedSessionLocked)
                {
                    SetupPasswordPanel();
                }
                else
                {
                    CinecastManager.Instance.PreparePlaybackService();
                    sessionManagementPanel.SetActive(false);
                }
            });
        }
        else
        {
            showPreviousSessionsButton.onClick.RemoveAllListeners();
            showPreviousSessionsButton.enabled = true;
            showPreviousSessionsButton.onClick.AddListener(() =>
            {
                newSessionSettingsPanel.SetActive(false);
                sessionManagementState = SessionManagementState.PreviousSessions;
                SetupSessionManagementPanel();
            });

            startNewSessionButton.onClick.RemoveAllListeners();
            startNewSessionButton.enabled = false;

            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(() =>
            {
                CinecastManager.Instance.SessionName = sessionNameInput.text;
                CinecastManager.Instance.SessionPassword = sessionPasswordInput.text;
                CinecastManager.Instance.AllowSpectorEvents = canSendSpecatorEvents;
                CinecastManager.Instance.CalculateInterest = calculateInterest;
                CinecastManager.Instance.PrepareRecordingService();
                sessionManagementPanel.SetActive(false);
            });
        }
    }

    private void SetupNewSessionPanel()
    {
        newSessionSettingsPanel.SetActive(true);

        sessionManagementState = SessionManagementState.NewSession;
        SetupSessionManagementPanel();

    }

    public void AddSessionButton(string sessionName, string sessionState, long sessionId, string sessionLength, bool isLocked)
    {
        GameObject sessionButton = Instantiate(sessionButtonPrefab);
        sessionButton.transform.SetParent(sessionScrollRect);

        SessionButton sessionButtonScript = sessionButton.GetComponent<SessionButton>();
        sessionButtonScript.sessionName.text = sessionName;
        sessionButtonScript.sessionState.text = sessionState;

        sessionButtonScript.sessionLength.text = sessionLength;

        sessionButtonScript.passwordStateImage.sprite = isLocked ? lockedSprite : unlockedSprite;

        sessionButtonScript.button.onClick.RemoveAllListeners();
        sessionButtonScript.button.onClick.AddListener(() =>
        {
                CinecastManager.Instance.SelectedSessionLocked = isLocked;
                CinecastManager.Instance.SelectedSessionPlaybackID = sessionId;
        });
    }

    public void RefreshSessionsPanel()
    {
        foreach(RectTransform child in sessionScrollRect.GetComponentInChildren<RectTransform>())
        {
            Destroy(child.gameObject);
        }
    }

    public void SetupPasswordPanel()
    {
        passwordPanel.SetActive(true);

        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(() =>
        {
            passwordPanel.SetActive(false);
        });

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(() =>
        {
            if(string.IsNullOrEmpty(passwordInput.text))
            {
                Debug.LogError("Password Required!");
            }
            else
            {
                CinecastManager.Instance.EnteredPassword = passwordInput.text;
                CinecastManager.Instance.PreparePlaybackService();
                passwordPanel.SetActive(false);
                sessionManagementPanel.SetActive(false);
            }
        });
    }

    private void UpdateRecordingUI()
    {
        currentStatsText.text = string.Format("{0} Hiders remaining!",DemoManager.Instance.ActiveHiders.Count);
        watcherText.text = string.Format("{0} Spectators",CinecastManager.Instance.Spectators);
    }

    private void SetupRecordingUI()
    {
        recordingPanel.SetActive(true);

        watcherText.text = string.Format("{0} Spectators",CinecastManager.Instance.Spectators);
        currentStatsText.text = string.Format("{0} Hiders remaining!",DemoManager.Instance.ActiveHiders.Count);

        stopRecordingButton.onClick.RemoveAllListeners();
        stopRecordingButton.onClick.AddListener(() => {
            CinecastManager.Instance.StopRecording();
            recordingPanel.SetActive(false);
        });

        cameraViewDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> dropdownOptions = new List<TMP_Dropdown.OptionData>();

        for(int i = 1; i < CameraManager.Instance.cameraPresets.Count; i++)
        {
            TMP_Dropdown.OptionData data = new TMP_Dropdown.OptionData(CameraManager.Instance.cameraPresets[i].name);
            dropdownOptions.Add(data);
        }

        cameraViewDropdown.AddOptions(dropdownOptions);
        cameraViewDropdown.onValueChanged.RemoveAllListeners();
        cameraViewDropdown.onValueChanged.AddListener(delegate {
            CameraManager.Instance.ChangeCamera(CameraManager.Instance.cameraPresets[cameraViewDropdown.value + 1].name);
        });
        cameraViewDropdown.value = 0;

        interveneButton.onClick.RemoveAllListeners();
        interveneButton.onClick.AddListener(() => {
            foreach (Agent hider in DemoManager.Instance.ActiveHiders) {
                hider.Intervene();
            }
        });
    }



    private void UpdatePlaybackUI()
    {
        spectatorText.text = CinecastManager.Instance.PlaybackIsLive ? $"{CinecastManager.Instance.Spectators} Spectators" : "";
        
        hidersRemainingText.text = $"{DemoManager.Instance.ActiveHiders.Count} Hiders remaining!";

        if(CinecastManager.Instance.playbackState == PlaybackState.Playing)
        {
            timelineSlider.SetValueWithoutNotify(CinecastManager.Instance.CurrentFrame);
            timeText.text = CinecastManager.Instance.CurrentTime;
        }
    }

    private void SetupPlaybackUI()
    {
        playbackPanel.SetActive(true);

        timelineSlider.minValue = 1;
        timelineSlider.maxValue = CinecastManager.Instance.TotalFrames;
        timelineSlider.value = CinecastManager.Instance.CurrentFrame;

        timelineSlider.onValueChanged.RemoveAllListeners();
        timelineSlider.onValueChanged.AddListener(delegate
        {
            CinecastManager.Instance.SeekToFrame((int)timelineSlider.value);
            UpdatePOIButtons();
        });

        playPauseButton.onClick.RemoveAllListeners();
        playPauseButton.onClick.AddListener(() =>
        {
            if (CinecastManager.Instance.playbackState == PlaybackState.Paused)
            {
                SetupPOIPanel();
                DemoManager.Instance.ResetAgents();
                CinecastManager.Instance.playbackState = PlaybackState.Playing;
                playPauseButton.GetComponent<Image>().sprite = pauseSprite;
            }
            else
            {
                CinecastManager.Instance.playbackState = PlaybackState.Paused;
                DemoManager.Instance.StopAgents();
                playPauseButton.GetComponent<Image>().sprite = playSprite;
            }
        });

        backToMainMenuButton.onClick.RemoveAllListeners();
        backToMainMenuButton.onClick.AddListener(() =>
        {
            playbackPanel.SetActive(false);
            CinecastManager.Instance.StopPlayBack();
            DemoManager.Instance.SwitchDemoState(DemoState.MainMenu);
        });
    }

    public void SessionEnded()
    {
        playPauseButton.GetComponent<Image>().sprite = playSprite;

        sessionEndedPanel.SetActive(true);

        stayInPlaybackModeButton.onClick.RemoveAllListeners();
        stayInPlaybackModeButton.onClick.AddListener(() =>
        {
            sessionEndedPanel.SetActive(false);
        });

        backToMainButton.onClick.RemoveAllListeners();
        backToMainButton.onClick.AddListener(() =>
        {
            sessionEndedPanel.SetActive(false);
            playbackPanel.SetActive(false);
            CinecastManager.Instance.StopPlayBack();
            DemoManager.Instance.SwitchDemoState(DemoState.MainMenu);
        });
    }

    public void SetupPOIPanel()
    {
        foreach(RectTransform child in poiPanel.GetComponentInChildren<RectTransform>())
        {
            Destroy(child.gameObject);
        }
        
        foreach(var entry in CinecastManager.Instance.CurrentPoiStates)
        {
            GameObject poiItem = Instantiate(poiItemPrefab,poiPanel.transform);
            POIItemUI poiItemScript = poiItem.GetComponent<POIItemUI>();
            poiItemScript.itemName.text = entry.Value.Name;
            poiItemScript.interstFillImage.fillAmount = 0;

            float interest = Mathf.RoundToInt(CinecastManager.Instance.GetInterestPerPoi(entry.Value.Name));
            poiItemScript.interestValue.text = $"Interest: {interest}";

            poiItemScript.button.onClick.RemoveAllListeners();
            poiItemScript.button.onClick.AddListener(() =>
            {
                CameraManager.Instance.ChangeCamera(entry.Value.Name);
            });
        }
    }

    private void UpdatePOIButtons()
    {
       foreach(RectTransform child in poiPanel.GetComponentInChildren<RectTransform>())
       {
           POIItemUI itemUI = child.GetComponent<POIItemUI>();
           float interest = Mathf.RoundToInt(CinecastManager.Instance.GetInterestPerPoi(itemUI.itemName.text));
           itemUI.interestValue.text = $"Interest: {interest}";
           var interestPercent = Mathf.InverseLerp(1f, 2000f, interest);
           itemUI.interstFillImage.fillAmount = interestPercent;
           itemUI.interstFillImage.color = poiGradient.Evaluate(interestPercent);

           Agent agent = DemoManager.Instance.AllAgents.Find(x => x.AgentId == itemUI.itemName.text);
           if(agent != null)
           {
               if(agent.CurrentTrigger == "Death")
               {
                   itemUI.poiDeadImage.gameObject.SetActive(true);
               }
           }
       }
    }

    public void AnnounceItemDrop(string itemName)
    {

        if(DemoManager.Instance.demoState == DemoState.Recording)
        {
            spectatorItemDropTextRecording.gameObject.SetActive(true);
            spectatorItemDropTextRecording.text = string.Format("{0} has just been dropped!",itemName);
        }
        else
        {
            spectatorItemDropTextPlayback.gameObject.SetActive(true);
            spectatorItemDropTextPlayback.text = string.Format("{0} has just been dropped!",itemName);
        }

        notificationTimer = Time.time;
        notificationShow = true;
    }

    public void SetupSpecatorEventsPanel()
    {

        if(CinecastManager.Instance.SpectatorEvents == null || !CinecastManager.Instance.AllowSpectorEvents)
        {
            specatorEventPanel.SetActive(false);
            return;
        }

        specatorEventPanel.SetActive(true);
        foreach(RectTransform child in specatorEventPanel.GetComponentInChildren<RectTransform>())
        {
            Destroy(child.gameObject);
        }

        foreach(var item in CinecastManager.Instance.SpectatorEvents)
        {
            GameObject spectatorItem = Instantiate(spectatorEventPrefab, specatorEventPanel.transform);
            TextMeshProUGUI spectatorText = spectatorItem.GetComponentInChildren<TextMeshProUGUI>();
            Image spectatorImage = spectatorItem.GetComponentInChildren<Image>();
            spectatorImage.sprite = item.Name == "Invincible" ? invincibleSprite : magicRakeSprite;
            spectatorText.text = item.Name;

            spectatorItem.GetComponent<Button>().onClick.RemoveAllListeners();
            spectatorItem.GetComponent<Button>().onClick.AddListener(() =>
            {
                Cinecast.Api.Settings.ISpectatorEventRef eventRef = new Cinecast.Api.Settings.SpectatorEventRef(item.ReferenceId);
                CinecastManager.Instance.RequestSpectatorEvent(eventRef,Vector3.zero);
            });
        }
    }
}
