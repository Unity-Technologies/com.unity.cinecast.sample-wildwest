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
    
    [Header("SessionSearchTags:")] 
    public List<SampleTag> AllowedSessionSearchTags = new List<SampleTag>();

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

    private List<Cinecast.Api.Server.Extraction.SpectatorEvent> spectatorEventsTimeline;
    
    private List<SessionTag> SessionSearchTags = new List<SessionTag>();
    private List<SessionTag> NewSessionStartTags = new List<SessionTag>();

    #region Properties
    public static CinecastManager Instance { get; private set; }
    public CinecastState cinecastState { get; private set; }
    public string JWT { get; set; }

    public string SessionName { get; set; }
    public string SessionPassword { get; set; }
    public bool AllowSpectorEvents { get; set; }
    public string EnteredPassword { get; set; }
    public bool SelectedSessionLocked { get; set; }

    public long SelectedSessionPlaybackID { get; set; }
    public long CurrentFrame { get; private set; }
    public long TotalFrames { get; private set; }
    public PlaybackState playbackState { get; set; }

    public string CurrentTime { get; private set; }
    public Dictionary<string,PoiFrameInterest> CurrentInterestData { get; private set; }
    public IReadOnlyDictionary<string, IPoiItem> CurrentPoiStates { get; private set; }
    public IReadOnlyList<ClientSpectatorEvent> SpectatorEvents { get; private set; }
    public int Spectators { get; private set; }

    public bool PlaybackIsLive { get; private set; }
    public float CurrentSessionDuration { get; private set; }
    public float CurrentFrameTime { get; private set; }

    #endregion

    private void Awake()
    {
        Application.targetFrameRate = 30;
        
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
        
        GetRecentSessions();
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
    public async void GetRecentSessions()
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
    
    public async void GetSessionsByName(string searchInput)
    {
        if (sessionManagementService == null)
        {
            sessionManagementService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<ISessionManagementService>(DiscoveryOptions.Required).ConfigureAwait(false);
        }

        (bool success, SessionSearchResponse sessionSearchResponse) =
            await sessionManagementService.FindByName(searchInput).ConfigureAwait(false);

        mainThreadDispatcher.Dispatch(() =>
        {
            if (success)
            {
                UserInterface.Instance.RefreshSessionsPanel();
                if (sessionSearchResponse.Data == null) return;
                foreach (var item in sessionSearchResponse.Data)
                {
                    TimeSpan sessionDuration = item.EndAt.Subtract(item.CreatedAt);
                    string sessionLength = (item.Status == SessionInfoSearchResponse_Status.Recording)
                        ? "LIVE"
                        : $"{sessionDuration.Minutes:00}:{sessionDuration.Seconds:00}";
                    UserInterface.Instance.AddSessionButton(item.Name, item.Status.ToString(), item.Id, sessionLength,
                        item.IsLocked);
                }
            }
            else
            {
                Debug.Log($"No sessions with the name {searchInput} found!");
            }
        });
    }

    public void UpdateSessionSearchTags(List<SampleTag> currentSearchedTags)
    {
        SessionSearchTags.Clear();

        for (int i = 0; i < currentSearchedTags.Count; i++)
        {
            SessionTag tag = new SessionTag();
            tag.Category = currentSearchedTags[i].Category;
            tag.Tags = new List<string>();
            tag.Tags = currentSearchedTags[i].Tags;
            SessionSearchTags.Add(tag);
        }
        
        UpdateSessionSearch();
    }
    
    public void UpdateSessionStartTags(List<SampleTag> selectedSessionStartTags)
    {
        NewSessionStartTags.Clear();
        
        for (int i = 0; i < selectedSessionStartTags.Count; i++)
        {
            SessionTag tag = new SessionTag();
            tag.Category = selectedSessionStartTags[i].Category;
            tag.Tags = new List<string>();
            tag.Tags = selectedSessionStartTags[i].Tags;
            NewSessionStartTags.Add(tag);
        }
    }
    public async void UpdateSessionSearch()
    {
        if (sessionManagementService == null)
        {
            sessionManagementService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<ISessionManagementService>(DiscoveryOptions.Required).ConfigureAwait(false);
        }

        if (SessionSearchTags.Count == 0)
        {
            GetRecentSessions();
        }
        else
        {
            (bool success, SessionSearchResponse sessionSearchResponse) =
                await sessionManagementService.FindByTags(SessionSearchTags).ConfigureAwait(false);
        
            mainThreadDispatcher.Dispatch(() =>
            {
                if (success)
                {
                    UserInterface.Instance.RefreshSessionsPanel();
                    if (sessionSearchResponse.Data == null) return;
                    foreach (var item in sessionSearchResponse.Data)
                    {
                        TimeSpan sessionDuration = item.EndAt.Subtract(item.CreatedAt);
                        string sessionLength = (item.Status == SessionInfoSearchResponse_Status.Recording)
                            ? "LIVE"
                            : $"{sessionDuration.Minutes:00}:{sessionDuration.Seconds:00}";
                        UserInterface.Instance.AddSessionButton(item.Name, item.Status.ToString(), item.Id, sessionLength,
                            item.IsLocked);
                    }
                }
                else
                {
                    Debug.Log($"No sessions with these tags found!");
                }
            });
        }
    }

    public async void DeleteSession(long sessionId)
    {
        await sessionManagementService.DeleteSession(sessionId).ConfigureAwait(false);
        GetRecentSessions();

        if (showCinecastLogs)
        {
            Debug.Log($"Successfully deleted session {sessionId}!");
        }
    }
    
    
#endregion
#region Recording
    public async void PrepareRecordingService()
    {
        recordingService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IRecordingService>(DiscoveryOptions.Required).ConfigureAwait(false);
        appSettingsService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IAppSettingsService>(DiscoveryOptions.Required).ConfigureAwait(false);
        watcherService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IWatcherService>(DiscoveryOptions.Required).ConfigureAwait(false);
        poiRecordingService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IPoiRecordingService>(DiscoveryOptions.Required).ConfigureAwait(false);
        appSettings = Cinecast.Generated.AppSettings_Basic.AppSetting;
    
        CreateInitialPOIs();
        watcherService.OnWatcherCountsUpdated += OnWatcherCountsUpdated;

        if(showCinecastLogs)
        {
            Debug.Log("Appsettings Service Started!");
        }
    

        if(AllowSpectorEvents)
        {
            PrepareSpectatorEventRecordingService();
        }

        if(showCinecastLogs)
        {
            Debug.Log("Recording Services Prepared.");
        }

        mainThreadDispatcher.Dispatch(() =>
        {
            DemoManager.Instance.SwitchDemoState(DemoState.Recording);
        });
        StartRecording();
    }

    public async void StartRecording()
    {
        string applicationVersion = @"1.0.0";

        SessionName = string.IsNullOrEmpty(SessionName) ? "mySession" + Time.time : SessionName;
        string password = string.IsNullOrEmpty(SessionPassword) ? null : SessionPassword;
        
        IList<SessionSpectatorEventSetting> sessionSpectatorEvents = AllowSpectorEvents ?  GetSpectatorEventSettings() : null;

        SessionRecordingStartRequest recordingRequest = new SessionRecordingStartRequest()
        {
            Name = SessionName,
            Password = password,
            ApplicationVersion = applicationVersion,
            SettingReferenceId = appSettings?.RefId,
            InterestCalc = SessionRecordingStartRequest_InterestCalc.Record,
            SpectatorEvents = sessionSpectatorEvents,
            SessionTags = NewSessionStartTags
        };

        (bool isRecording, RecordingSessionInfo recordingSessionInfo) =
            await recordingService.StartRecording(recordingRequest).ConfigureAwait(false);
        

        mainThreadDispatcher.Dispatch(() =>
        {
            if (isRecording)
            {
                cinecastState = CinecastState.Recording;
                DemoManager.Instance.StartAgents();
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
        });

    }

    public async void StopRecording()
    {
        cinecastState = CinecastState.Ready;
        
        await recordingService.StopRecording().ConfigureAwait(false);

        if(showCinecastLogs)
        {
            Debug.Log("Recording stopped.");
        }
        
        mainThreadDispatcher.Dispatch(()=>
        {
            DemoManager.Instance.StopAgents();
            DemoManager.Instance.SwitchDemoState(DemoState.MainMenu);
            DisposeRecordingServices();
        });
        
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
    
    public void DisposeRecordingServices()
    {
        SDKRuntimeCinecast.Instance.Discovery.Release(recordingService);
        recordingService = null;

        SDKRuntimeCinecast.Instance.Discovery.Release(appSettingsService);
        appSettingsService = null;
        appSettings = null;

        SDKRuntimeCinecast.Instance.Discovery.Release(watcherService);
        watcherService = null;

        SDKRuntimeCinecast.Instance.Discovery.Release(poiRecordingService);
        poiRecordingService = null;

        if (spectatorRecordingService != null)
        {
            SDKRuntimeCinecast.Instance.Discovery.Release(spectatorRecordingService);
            spectatorRecordingService = null;
        }
    }
#endregion
#region Playback
    public async void PreparePlaybackServices()
    {
        playbackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(false);
        playbackService.UpdateSettings(cinecastConfig.PlaybackConfig);

        if(showCinecastLogs)
        {
            Debug.Log("Playback Services Prepared.");
        }

        appSettingsService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IAppSettingsService>(DiscoveryOptions.Required).ConfigureAwait(false);
        appSettings = appSettings = Cinecast.Generated.AppSettings_Basic.AppSetting;
        if(showCinecastLogs)
        {
            Debug.Log("Appsettings Service Started!");
        }
    
        //Interest Playback Service
        interestPlaybackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IInterestPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(false);

        //Spectator Service
        spectatorPlaybackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<ISpectatorPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(false);

        if (showCinecastLogs)
        {
            Debug.Log("Spectator Playback Service Started");
        }

        GetSpectatorTimeline();
        spectatorPlaybackService.OnSpectatorEventInventoryUpdated += OnSpectatorEventInventoryUpdated;
        spectatorPlaybackService.OnSpectatorEventTimelineUpdated += OnSpectatorEventsTimelineUpdated;
        SpectatorEvents = appSettingsService.GetSpectatorEvents(appSettings);
    
        watcherService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IWatcherService>(DiscoveryOptions.Required).ConfigureAwait(false);
        watcherService.OnWatcherCountsUpdated += OnWatcherCountsUpdated;
    
        poiPlaybackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<IPoiPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(false);

        poiPlaybackService.OnPoiVersionUpdated += OnPOIVersionUpdate;
    
        GetPOIStates();
        InitiatePlayback();
    }

    private async void InitiatePlayback()
    {
        string password = string.IsNullOrEmpty(EnteredPassword) ? null : EnteredPassword;
        (_, PlaybackSessionInfo playbackSessionInfo) = await playbackService.StartPlayback(SelectedSessionPlaybackID, @"1.0.0", null, password).ConfigureAwait(false);

        CurrentSessionDuration = (float) playbackSessionInfo.EndTime.TotalSeconds;

        PlaybackIsLive = !playbackSessionInfo.IsComplete;
        if (PlaybackIsLive)
        {
            await playbackService.PreloadLiveFrames().ConfigureAwait(false);
        }
        else
        {
            await playbackService.PreloadFromFrame(1).ConfigureAwait(false);
        }

        TotalFrames = playbackSessionInfo.TotalFrames;
        Debug.Log($"End time = {playbackSessionInfo.EndTime} and endindex = {playbackSessionInfo.EndIndex} and total frames = {playbackSessionInfo.TotalFrames}");
        CurrentFrame = 1;

        mainThreadDispatcher.Dispatch(() =>
        {
            // Get first frame so POIs can be set up
            playbackService.GetNextFrame();
            UpdateInterest();
            DemoManager.Instance.SwitchDemoState(DemoState.Playback);
            playbackState = PlaybackState.Playing;
            
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
                CurrentFrameTime = (float) frameInfo.FrameTime.TotalSeconds;
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
        playbackState = PlaybackState.Paused;
        await playbackService.StopPlayback().ConfigureAwait(false);

        mainThreadDispatcher.Dispatch(() =>
        {
            DemoManager.Instance.StopAgents();
        });
        
        cinecastState = CinecastState.Ready;
    }
    
    public void DisposePlaybackServices()
    {
        SDKRuntimeCinecast.Instance.Discovery.Release(playbackService);
        playbackService = null;

        SDKRuntimeCinecast.Instance.Discovery.Release(appSettingsService);
        appSettingsService = null;

        SDKRuntimeCinecast.Instance.Discovery.Release(interestPlaybackService);
        interestPlaybackService = null;

        SDKRuntimeCinecast.Instance.Discovery.Release(spectatorPlaybackService);
        spectatorPlaybackService = null;

        SDKRuntimeCinecast.Instance.Discovery.Release(watcherService);
        watcherService = null;

        SDKRuntimeCinecast.Instance.Discovery.Release(poiPlaybackService);
        poiPlaybackService = null;
    }
#endregion
#region Interest
public float GetInterestPerPoi(string name)
    {
        if (CurrentInterestData == null)
        {
            return 0f;
        }

        PoiFrameInterest frameInterest;
        if (CurrentInterestData.TryGetValue(name, out frameInterest))
        {
            return frameInterest.FrameData.Interest;
        }
        else
        {
            return 0f;
        }
    }

    void UpdateInterest()
    {
        if (CurrentInterestData == null)
        {
            CurrentInterestData = new Dictionary<string, PoiFrameInterest>();
        }

        for (int i = 0; i < DemoManager.Instance.AllAgents.Count; i++)
        {
            string name = DemoManager.Instance.AllAgents[i].AgentId;
            PoiFrameInterest frameInterest = GetInterest(name, CurrentFrame);

            if (frameInterest!= null)
            {
                if (CurrentInterestData.ContainsKey(name))
                {
                    CurrentInterestData[name] = frameInterest;
                }
                else
                {
                    CurrentInterestData.Add(name, frameInterest);
                }
            }
        }
    }

    private PoiFrameInterest GetInterest(string id, long frameIndex)
    {
        IReadOnlyDictionary<string, PoiFrameInterest> frameInterests;
        if (interestPlaybackService == null)
        {
            Debug.LogError("INTEREST SERVICE IS NULL!!!");
        }
        interestPlaybackService.GetFrameInterest(frameIndex, out frameInterests);
        
        if (frameInterests == null)
        {
            return null;
        }
        if (frameInterests.TryGetValue(id, out PoiFrameInterest interest))
        {
            return interest;
        }

        return null;
    }
#endregion
#region POI's
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
        spectatorRecordingService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<ISpectatorRecordingService>(DiscoveryOptions.Required).ConfigureAwait(false);

        spectatorRecordingService.OnIncomingRequestEvents += OnIncomingSpectatorEventsRequest;

        if(showCinecastLogs)
        {
            Debug.Log("Spectator Recording Service Started");
        }
    }

    public async void PrepareSpectatorPlaybackService()
    {
        spectatorPlaybackService = await SDKRuntimeCinecast.Instance.Discovery.Resolve<ISpectatorPlaybackService>(DiscoveryOptions.Required).ConfigureAwait(false);

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
        IReadOnlyList<ClientSpectatorEvent> settings = appSettingsService.GetSpectatorEvents(appSettings);
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

    private void OnDestroy()
    {
        SDKRuntimeCinecast.Instance.Dispose();
    }
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

