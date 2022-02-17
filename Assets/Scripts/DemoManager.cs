using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoManager : MonoBehaviour
{
    [Header("Demo Settings")]
    public Agent seeker;
    public List<Agent> hiders;

    public GameObject magicRakePrefab;
    public GameObject invinciblePotionPrefab;

    public static DemoManager Instance { get; private set; }
    public DemoState demoState { get; private set; }
    public List<Agent> ActiveHiders { get; private set; }
    public List<Agent> AllAgents { get; private set; }

    public event System.Action<DemoState> onDemoStateChanged;

    private void Awake()
    {
        if(Instance == null) {
            Instance = this;
            GameObject.DontDestroyOnLoad(this.gameObject);
        } else {
            Destroy(gameObject);
        }

        AllAgents = new List<Agent>();
        AllAgents.AddRange(hiders);
        AllAgents.Add(seeker);

        demoState = DemoState.MainMenu;
        SetupMainMenu();
    }
    public void SwitchDemoState(DemoState state)
    {
        demoState = state;
        onDemoStateChanged?.Invoke(demoState);
    }

    public void SetupMainMenu()
    {
        seeker.transform.position = seeker.mainMenuTransform.position;

        for(int i = 0; i< hiders.Count; i++)
        {
            hiders[i].transform.position = hiders[i].mainMenuTransform.position;
        }
    }

    public void SetupRecording()
    {
        seeker.transform.position = seeker.sessionStartTransform.position;
        ActiveHiders = new List<Agent>();

        for(int i = 0; i< hiders.Count; i++)
        {
            hiders[i].transform.position = hiders[i].sessionStartTransform.position;
            ActiveHiders.Add(hiders[i]);
        }
    }

    public void SetupPlayback()
    {
        seeker.transform.position = seeker.sessionStartTransform.position;
        ActiveHiders = new List<Agent>();

        for(int i = 0; i< hiders.Count; i++)
        {
            hiders[i].transform.position = hiders[i].sessionStartTransform.position;
            ActiveHiders.Add(hiders[i]);
        }
    }

    public void StartAgents()
    {
        for(int i = 0; i < AllAgents.Count; i++)
        {
            AllAgents[i].StartAgent();
            AllAgents[i].ResetAnimator();
        }
    }

    public void ResetAgents()
    {
        for(int i = 0; i < AllAgents.Count; i++)
        {
            AllAgents[i].ResetAnimator();
        }
    }

    public void PauseAgents()
    {
        for(int i = 0; i < AllAgents.Count; i++)
        {
            AllAgents[i].StopAgent();
            AllAgents[i].ResetAnimator();
        }
    }

    public void StopAgents()
    {
        for(int i = 0; i < AllAgents.Count; i++)
        {
            AllAgents[i].StopAgent();
            AllAgents[i].ResetAnimator();
            AllAgents[i].transform.position = AllAgents[i].sessionStartTransform.position;
            AllAgents[i].transform.rotation = AllAgents[i].sessionStartTransform.rotation;
        }
    }

    public void ItemDrop(string referenceId)
    {
        Vector3 itemDropLocation = new Vector3(0f,2f,6f) + (Vector3.up * 20f);
        GameObject itemGO = referenceId == "magicRake" ? Instantiate(magicRakePrefab,itemDropLocation,Quaternion.identity) : Instantiate(invinciblePotionPrefab, itemDropLocation, Quaternion.identity);
        itemGO.GetComponent<Rigidbody>().useGravity = true;

        UserInterface.Instance.AnnounceItemDrop(referenceId);
    }



}

public enum DemoState
{
    MainMenu,
    Recording,
    Playback
}