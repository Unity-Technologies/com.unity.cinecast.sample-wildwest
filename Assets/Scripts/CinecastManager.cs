using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Cinecast.Implementation.Application;
using Cinecast.Implementation.Configs;
using Cinecast.Api.Recording;
using Cinecast.Api.Playback;
using Cinecast.Api.Settings;
using Cinecast.Core.Api.Discovery;
using Cinecast.Core.Api.Threading;
using Cinecast.Api.Sessions;
using Cinecast.Api.Server.Api;
using Cinecast.Api.Poi;
using Cinecast.Api.Spectator;
using Cinecast.Api.Server.Extraction;
using Cinecast.Core.Api.Networking;

public class CinecastManager : MonoBehaviour
{
    [Header("Debugging:")]
    [SerializeField] public bool IgnoreAuthorization;
    [SerializeField] private bool showCinecastLogs;

    [Header("Authorization")]
    [SerializeField] private string publicKey;
    [SerializeField] private string secretKey;

    [Header("Config:")]
    [SerializeField] private CinecastConfig cinecastConfig;

    private bool authorizationComplete;

    private IAppSettingsService appSettingsService;
    private IAppSettingRef appSettings;

    private int currentPoiVersion;

    private IMainThreadDispatcher mainThreadDispatcher;

    private ISessionManagementService sessionManagementService;

    private IRecordingService recordingService;
    private IPlaybackService playbackService;
    private IPoiRecordingService poiRecordingService;
    private IPoiPlaybackService poiPlaybackService;
    private ISpectatorPlaybackService spectatorPlaybackService;
    private ISpectatorRecordingService spectatorRecordingService;
    private IInterestPlaybackService interestPlaybackService;
    private IWatcherService watcherService;

    private bool currentSessionIsLive;

    private long lastFrame;

    private List<Cinecast.Api.Server.Extraction.SpectatorEvent> spectatorEventsTimeline;

    #region Properties
    public static CinecastManager Instance { get; private set; }
    public CinecastState cinecastState { get; private set; }
    public string JWT { get; set; }

    public string SessionName { get; set; }
    public string SessionPassword { get; set; }
    public bool AllowSpectorEvents { get; set; }
    public bool CalculateInterest { get; set; }
    public string EnteredPassword { get; set; }
    public bool SelectedSessionLocked { get; set; }

    public long SelectedSessionPlaybackID { get; set; }
    public long CurrentFrame { get; private set; }
    public long TotalFrames { get; private set; }
    public PlaybackState playbackState { get; set; }

    public string CurrentTime { get; private set; }
    public Dictionary<string,IPoiInterestData> CurrentInterestData { get; private set; }
    public IReadOnlyDictionary<string, IPoiItem> CurrentPoiStates { get; private set; }
    public IReadOnlyList<ClientSpectatorEventSetting> SpectatorEvents { get; private set; }
    public int Spectators { get; private set; }

    public bool PlaybackIsLive { get; private set; }

    #endregion

    private void Awake()
    {
        lastFrame = 0;
        Application.targetFrameRate = 60;
        
        if(Instance == null){
            Instance = this;
            GameObject.DontDestroyOnLoad(this.gameObject);
            authorizationComplete = false;
        } else {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        SetFrameState();
        RecordFrame();
    }


    public bool HasPersonalKey()
    {
        return !string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(secretKey);
    }

#region Cinecast Startup
    public async void StartupCinecast()
    {
        if(authorizationComplete)
        {
            return;
        }
        
        SDKRuntimeCinecast.Instance.OnStartupComplete += OnStartupComplete;

        await SDKRuntimeCinecast.Instance.Startup(cinecastConfig,AuthHeaderProvider).ConfigureAwait(false);
        mainThreadDispatcher = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IMainThreadDispatcher>(DiscoveryOptions.Required).ConfigureAwait(false);
        
        await SDKRuntimeCinecast.Instance.Discovery.UsingResolve<IAuthorizationService>(service =>
        {
            service.OnAuthorizationFailed += OnAuthorizationFailed;
        }).ConfigureAwait(false);
    }

    private void OnStartupComplete(object sender, EventArgs args)
    {
        SDKRuntimeCinecast.Instance.OnStartupComplete -= OnStartupComplete;
        authorizationComplete = true;

        if(showCinecastLogs){
            Debug.Log("Cinecast Authorization Successful!");
        }
        GetSessions();
    }

    private void OnAuthorizationFailed(object sender, INetworkResult result)
    {
        if (showCinecastLogs)
        {
            Debug.Log("Cinecast authorization failed!");
        }
    }

    private string AuthHeaderProvider()
    {
        return !HasPersonalKey() ? $@"Bearer {JWT}" : $@"Basic {Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($@"{publicKey}:{secretKey}"))}";
    }
#endregion
#region Sessionmanagement
    public async void GetSessions()
    {
        sessionManagementService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<ISessionManagementService>(DiscoveryOptions.Required).ConfigureAwait(false);

        int requestedSessionsAmount = 5;

        (bool success, SessionSearchResponse sessionSearchResponse) = await sessionManagementService.GetLatest(requestedSessionsAmount);
        if(success){
            mainThreadDispatcher.Dispatch(() =>{
                if(showCinecastLogs){
                    Debug.Log("Retrieved previous sessions successfully!");
                }

                UserInterface.Instance.RefreshSessionsPanel();
                if(sessionSearchResponse.Data != null)
                {
                    //Setting a default session to play in case none is selected
                    SelectedSessionPlaybackID = sessionSearchResponse.Data[0].Id;
                    if (sessionSearchResponse.Data[0].IsLocked)
                    {
                        SelectedSessionLocked = true;
                    }
                    
                    foreach (var item in sessionSearchResponse.Data)
                    {
                        TimeSpan sessionDuration = item.EndAt.Subtract(item.CreatedAt);
                        string sessionLength = (item.Status == SessionInfoSearchResponse_Status.Recording) ? "LIVE" : $"{sessionDuration.Minutes:00}:{sessionDuration.Seconds:00}";
                        UserInterface.Instance.AddSessionButton(item.Name, item.Status.ToString(), item.Id, sessionLength, item.IsLocked);
                    }
                }
            });
        }
    }
    
    
#endregion
#region AppSettings
    private async void PrepareAppSettingsService()
    {
        appSettingsService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IAppSettingsService>(DiscoveryOptions.Required).ConfigureAwait(true);

        if(showCinecastLogs)
        {
            Debug.Log("Appsettings Service Started!");
        }

        appSettings = Cinecast.Generated.AppSettings_Basic.AppSetting;
    }
#endregion
#region Recording
    public async void PrepareRecordingService()
    {
        recordingService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IRecordingService>(DiscoveryOptions.Required).ConfigureAwait(true);

        PrepareAppSettingsService();
        PrepareWatcherService();

        if(CalculateInterest)
        {
            PreparePoiRecordingService();
        }

        if(AllowSpectorEvents)
        {
            PrepareSpectatorEventRecordingService();
        }

        if(showCinecastLogs)
        {
            Debug.Log("Recording Services Prepared.");
        }

        DemoManager.Instance.SwitchDemoState(DemoState.Recording);
    }

    public async void StartRecording()
    {
        const string applicationVersion = @"1.0.0";

        SessionName = string.IsNullOrEmpty(SessionName) ? "mySession" + Time.time : SessionName;
        string password = (string.IsNullOrEmpty(SessionPassword) ? null : SessionPassword);


        IList<SessionSpectatorEventSetting> sessionSpectatorEvents = (AllowSpectorEvents) ?  GetSpectatorEventSettings() : null;

        SessionRecordingStartRequest_InterestCalc interestCalc = (CalculateInterest) ? SessionRecordingStartRequest_InterestCalc.Record : SessionRecordingStartRequest_InterestCalc.None;
        (bool isRecording, RecordingSessionInfo recordingSessionInfo) = await recordingService.StartRecording
        (SessionName,
        applicationVersion,
        null,
        password,
        appSettingRef: appSettings,
        interestCalc,
        sessionSpectatorEvents).ConfigureAwait(false);

        if (isRecording)
        {
            cinecastState = CinecastState.Recording;
            if(showCinecastLogs)
            {
                Debug.Log("Recording Started Successfully.");
            }
        }
        else
        {
            if(showCinecastLogs)
            {
                Debug.Log("Recording failed.");
            }
        }

        mainThreadDispatcher.Dispatch(() =>
        {
            DemoManager.Instance.StartAgents();
        });

    }

    public async void StopRecording()
    {
        cinecastState = CinecastState.Ready;

        await recordingService.StopRecording().ConfigureAwait(true);

        if(showCinecastLogs)
        {
            Debug.Log("Recording stopped.");
        }

        DemoManager.Instance.StopAgents();
        DemoManager.Instance.SwitchDemoState(DemoState.MainMenu);
    }

    private async void RecordFrame()
    {
        if (cinecastState == CinecastState.Recording)
        {
            SnapShot snapShot = new SnapShot{
                agentDatas = new AgentData[DemoManager.Instance.AllAgents.Count]
            };

            for(int i = 0; i < DemoManager.Instance.AllAgents.Count; i++)
            { 
                Agent agent = DemoManager.Instance.AllAgents[i];
                snapShot.agentDatas[i] = agent.GetAgentData();
            }

            string json = JsonUtility.ToJson(snapShot);
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

            (RecordingState recordingState, long lastFrame) = await recordingService.RecordAsync(data).ConfigureAwait(false);
        }
    }
#endregion
#region Playback
    public async void PreparePlaybackService()
    {
        playbackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(false);
        playbackService.UpdateSettings(cinecastConfig.PlaybackConfig);

        if(showCinecastLogs)
        {
            Debug.Log("Playback Services Prepared.");
        }

        PrepareAppSettingsService();
        PrepareInterestPlaybackService();
        PreparePoiPlaybackService();
        PrepareSpectatorPlaybackService();
        PrepareWatcherService();
        InitiatePlayback();
    }

    private async void InitiatePlayback()
    {
        string password = string.IsNullOrEmpty(EnteredPassword) ? null : EnteredPassword;
        (_, PlaybackSessionInfo playbackSessionInfo) = await playbackService.StartPlayback(SelectedSessionPlaybackID, @"1.0.0", null, password).ConfigureAwait(false);

        PlaybackIsLive = !playbackSessionInfo.IsComplete;
        if (PlaybackIsLive)
        {
            await playbackService.PreloadLiveFrames();
        }
        else
        {
            await playbackService.PreloadFromFrame(1);
        }

        TotalFrames = playbackSessionInfo.TotalFrames;
        CurrentFrame = 1;

        mainThreadDispatcher.Dispatch(() =>
        {
            // Get first frame so POIs can be set up
            playbackService.GetNextFrame();
            DemoManager.Instance.SwitchDemoState(DemoState.Playback);
            // Autoplay live session
            if(PlaybackIsLive)
            {
                playbackState = PlaybackState.Playing;
            }
        });
    }

    public void SeekToFrame(int frame)
    {
        playbackService.SeekToFrame(frame);
        SetFrameState();
    }
    
    public void SetFrameState()
    {
        if (playbackState == PlaybackState.Playing)
        {
            (bool success, FrameInfo frameInfo) = playbackService.GetNextFrame();
            if (success)
            {
                interestPlaybackService.GetRange();
                cinecastState = CinecastState.Playback;
                
                if (spectatorEventsTimeline != null)
                {
                    SpectatorEvent spectatorEvent = spectatorEventsTimeline.Find(x => x.EventFrame == frameInfo.Frame);
                    if (spectatorEvent != null)
                    {
                        DemoManager.Instance.ItemDrop(spectatorEvent.ReferenceId);
                    }
                }

                string json = System.Text.Encoding.UTF8.GetString(frameInfo.Snapshot);
                SnapShot snapShot = JsonUtility.FromJson<SnapShot>(json);
                for (int i = 0; i < snapShot.agentDatas.Length; i++)
                {
                    Agent currentAgent =
                        DemoManager.Instance.AllAgents.Find(agent => agent.AgentId == snapShot.agentDatas[i].id);
                    currentAgent.PlaybackFrame(snapShot.agentDatas[i]);
                }

                CurrentFrame = frameInfo.Frame;
                CurrentTime = $"{frameInfo.FrameTime.Minutes:00}:{frameInfo.FrameTime.Seconds:00}";
                if (CurrentFrame - lastFrame != 1)
                {
                    // Debug.LogError("Current Frame: " +m_CurrentFrame + " last frame:" + lastFrame + " Time: " + m_CurrentTime);
                }

                lastFrame = CurrentFrame;
                UpdateInterest();


                if (frameInfo.IsLastFrame)
                {
                    Debug.Log("Recording is over!");
                    DemoManager.Instance.StopAgents();
                    UserInterface.Instance.SessionEnded();
                    playbackState = PlaybackState.Paused;
                    cinecastState = CinecastState.Ready;

                }
            }
            else
            {
                if (showCinecastLogs)
                {
                    Debug.Log("We ran out ouf frames but this wasnt the last frame");
                }

                DemoManager.Instance.StopAgents();
                playbackState = PlaybackState.Paused;
                cinecastState = CinecastState.Ready;
            }
        }
    }

    public async void StopPlayBack()
    {
        await playbackService.StopPlayback();
        DemoManager.Instance.StopAgents();

        playbackState = PlaybackState.Paused;
        cinecastState = CinecastState.Ready;
    }
#endregion
#region Interest
    public async void PrepareInterestPlaybackService()
    {
        interestPlaybackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IInterestPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(false);

        interestPlaybackService.OnRangeUpdated += OnRangeUpdated;

        if(showCinecastLogs)
        {
            Debug.Log("Interest Playback Service Prepared!");
        }


    }

    public float GetInterestPerPoi(string name)
    {
        if(CurrentInterestData == null)
        {
            return 0f;
        }

        IPoiInterestData interestData;
        CurrentInterestData.TryGetValue(name, out interestData);

        return (interestData != null) ? interestData.MinInterest : 0f;
    }

    void UpdateInterest()
    {
        if (CurrentInterestData == null)
        {
            CurrentInterestData = new Dictionary<string, IPoiInterestData>();
        }

        for (int i = 0; i < DemoManager.Instance.AllAgents.Count; i++)
        {
            string name = DemoManager.Instance.AllAgents[i].AgentId;
            IPoiInterestData interestData = GetInterestPlayback(name, CurrentFrame);

            if (interestData != null)
            {
                if (CurrentInterestData.ContainsKey(name))
                {
                    CurrentInterestData[name] = interestData;
                }
                else
                {
                    CurrentInterestData.Add(name, interestData);
                }
            }
        }
    }

    private void OnRangeUpdated(object sender, EventArgs e)
    {
     
    
    }

    private IPoiInterestData GetInterestPlayback(string id, long frameIndex)
    {
        InterestFrameRange frameRange = interestPlaybackService.GetRange();
        IPoiInterestData interestData = default;

        if(frameRange.Ranges.TryGetValue(id,out InterestSnapshotRange snapshotRange))
        {
            interestData = snapshotRange.GetSnapshot(frameIndex);
        }

        return interestData;
    }
#endregion
#region POI's
    public async void PreparePoiRecordingService()
    {
        poiRecordingService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IPoiRecordingService>(DiscoveryOptions.Required).ConfigureAwait(true);
        CreateInitialPOIs();
    }

    public async void PreparePoiPlaybackService()
    {
        poiPlaybackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IPoiPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(true);

        poiPlaybackService.OnPoiVersionUpdated += OnPOIVersionUpdate;
        GetPOIStates();
    }

    private void GetPOIStates()
    {
        currentPoiVersion = poiPlaybackService.CurrentPoiVersion;
        CurrentPoiStates = poiPlaybackService.GetPoiState();
    }

    private void CreateInitialPOIs()
    {
        if (appSettings == null)
        {
            Debug.Log("Missing AppSettings");
            return;
        }

        //Register POIs
        IList<PoiDefinition> initialPois = new List<PoiDefinition>();

        if (appSettings.Equals(Cinecast.Generated.AppSettings_Basic.AppSetting))
        {
            string name = "WorldView";
            string id = name;
            IPoiTypeRef poiType = Cinecast.Generated.AppSettings_Basic.PoiTypes.Player.PoiType;

            PoiDefinition newPoi = new PoiDefinition(name,id,poiType);
            initialPois.Add(newPoi);

            name = "Seeker";
            id = DemoManager.Instance.seeker.AgentId;
            poiType = Cinecast.Generated.AppSettings_Basic.PoiTypes.Player.PoiType;
            newPoi = new PoiDefinition(name, id, poiType);
            initialPois.Add(newPoi);


            for (int h = 0; h < DemoManager.Instance.hiders.Count; h++)
            {
                name = DemoManager.Instance.hiders[h].AgentId;
                id = DemoManager.Instance.hiders[h].AgentId;
                poiType = Cinecast.Generated.AppSettings_Basic.PoiTypes.Player.PoiType;

                newPoi = new PoiDefinition(name, id, poiType);
                newPoi.Radius = 255f;
                initialPois.Add(newPoi);
            }
        }

        if (initialPois.Count > 0)
        {
            poiRecordingService.AddPois(initialPois);
        }

        if(showCinecastLogs)
        {
            Debug.Log("Initial POI's Setup Complete!");
        }
    }

    private void RecordPOIs()
    {
        for (int a = 0; a < DemoManager.Instance.AllAgents.Count; a++)
        {
            string agentId = DemoManager.Instance.AllAgents[a].AgentId.ToString();
            Vector3 position = DemoManager.Instance.AllAgents[a].gameObject.transform.position;
            Quaternion rotation = DemoManager.Instance.AllAgents[a].gameObject.transform.rotation;

            poiRecordingService.SetPosition(agentId, position);
            poiRecordingService.SetRotation(agentId, rotation);
        };
    }

    public void StartFleeingPOI(string agentId)
    {
        PoiEventRef poiEventRef = new PoiEventRef(@"flee");
        bool result = poiRecordingService.StartEvent(agentId,poiEventRef);
        if(result)
        {
        }
    }

    public void StartHuntingPOI(string agentId)
    {
        //TODO: Get your own config working
        PoiEventRef poiEventRef = new PoiEventRef(@"hunt");
        poiRecordingService.StartEvent(agentId,poiEventRef);
    }

    private void OnPOIVersionUpdate(object sender, int version)
    {
        if(version == currentPoiVersion)
        {
            return;
        }

        currentPoiVersion = version;
        CurrentPoiStates = poiPlaybackService.GetPoiState(currentPoiVersion);

        if(showCinecastLogs)
        {
            Debug.Log("POIs Updated!");
        }
    }

#endregion
#region Spectators
    private async void PrepareWatcherService()
    {
        watcherService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IWatcherService>(DiscoveryOptions.Required).ConfigureAwait(false);
        watcherService.OnWatcherCountsUpdated += OnWatcherCountsUpdated;
    }

    private void OnWatcherCountsUpdated(object sender, IReadOnlyDictionary<string,long> watcherCounts)
    {
        int watchers = 0;
        foreach(var count in watcherCounts)
        {
            watchers += (int) count.Value;
        }

        Spectators = watchers;
    }
#endregion
#region SpectatorEvents
    public async void PrepareSpectatorEventRecordingService()
    {
        spectatorRecordingService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<ISpectatorRecordingService>(DiscoveryOptions.Required).ConfigureAwait(true);

        spectatorRecordingService.OnIncomingRequestEvents += OnIncomingSpectatorEventsRequest;

        if(showCinecastLogs)
        {
            Debug.Log("Spectator Recording Service Started");
        }
    }

    public async void PrepareSpectatorPlaybackService()
    {
        spectatorPlaybackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<ISpectatorPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(true);

        if(showCinecastLogs)
        {
            Debug.Log("Spectator Playback Service Started");
        }

        GetSpectatorTimeline();
        spectatorPlaybackService.OnSpectatorEventInventoryUpdated += OnSpectatorEventInventoryUpdated;
        spectatorPlaybackService.OnSpectatorEventTimelineUpdated += OnSpectatorEventsTimelineUpdated;
    }

    public void GetSpectatorTimeline()
    {
        IReadOnlyList<Cinecast.Api.Server.Extraction.SpectatorEvent> list = spectatorPlaybackService.GetTimeline();
        spectatorEventsTimeline = new List<Cinecast.Api.Server.Extraction.SpectatorEvent>();
        spectatorEventsTimeline.AddRange(list);
    }

    private void OnSpectatorEventInventoryUpdated(object sender, IReadOnlyList<SpectatorEventInventory> inventory)
    {
        SpectatorEvents = appSettingsService.GetSpectatorEvents(appSettings);
    }

    private void OnSpectatorEventsTimelineUpdated(object sender, IReadOnlyList<Cinecast.Api.Server.Extraction.SpectatorEvent> spectatorEventsTimeline)
    {
        this.spectatorEventsTimeline = new List<Cinecast.Api.Server.Extraction.SpectatorEvent>();
        this.spectatorEventsTimeline.AddRange(spectatorEventsTimeline);
    }

    public async void RequestSpectatorEvent(ISpectatorEventRef eventRef, Vector3 position)
    {
        (bool success, Cinecast.Api.Server.Extraction.SpectatorEventResponse response) = await spectatorPlaybackService.TriggerSpectatorEvent(eventRef,position);
        if(success)
        {
            if(showCinecastLogs)
            {
                Debug.Log("Spectator Event Request Successfull");
            }
        }
    }

    private void OnIncomingSpectatorEventsRequest(object sender, IList<Cinecast.Api.Server.Ingestion.SpectatorEvent> spectatorEvents)
    {
        for(int i = 0; i < spectatorEvents.Count; i++)
        {
            if (AllowSpectorEvents)
            {
                AcceptSpectatorEvent(spectatorEvents[i]); 
            }
            else
            {
                DeclineSpectatorEvent(spectatorEvents[i].EventId);
            }
            
        }
    }

    private void AcceptSpectatorEvent(Cinecast.Api.Server.Ingestion.SpectatorEvent spectatorEvent)
    {
        bool success =  spectatorRecordingService.AcceptEvent(spectatorEvent.EventId);
        if(success)
        {
            if(showCinecastLogs)
            {
                Debug.Log("Accepting Spectator Event Successful");

            }

            mainThreadDispatcher.Dispatch(() => {
                DemoManager.Instance.ItemDrop(spectatorEvent.ReferenceId);
            });
        }
        else
        {
            if(showCinecastLogs)
            {
                Debug.Log("Accepting Spectator Event failed");
            }
        }
    }

    private void DeclineSpectatorEvent(string eventId)
    {
        bool success = spectatorRecordingService.DeclineEvent(eventId, "Event automatically declined!");
        if(success)
        {
            if(showCinecastLogs)
            {
                Debug.Log("Declining Spectator Event Successful");
            }
        }
        else
        {
            if(showCinecastLogs)
            {
                Debug.Log("Declining Spectator Event failed");
            }
        }
    }
    private IList<SessionSpectatorEventSetting> GetSpectatorEventSettings()
    {
        IReadOnlyList<ClientSpectatorEventSetting> settings = appSettingsService.GetSpectatorEvents(appSettings);
        IList<SessionSpectatorEventSetting> sessionSettings = new List<SessionSpectatorEventSetting>();

        foreach(var setting in settings)
        {
            SessionSpectatorEventSetting sessionSpectatorEventSetting = new SessionSpectatorEventSetting();
            sessionSpectatorEventSetting.EventReferenceId = setting.ReferenceId;
            sessionSpectatorEventSetting.SessionCoolDown = setting.SessionCoolDown;
            sessionSpectatorEventSetting.SessionMax = setting.SessionMax;
            sessionSpectatorEventSetting.UserCoolDown = setting.UserCoolDown;
            sessionSpectatorEventSetting.UserMax = setting.UserMax;

            sessionSettings.Add(sessionSpectatorEventSetting);
        }
        return sessionSettings;
    }
#endregion

}

public enum CinecastState
{
    Ready,
    Recording,
    Playback
}

public enum PlaybackState
{
    Paused,
    Playing
}

