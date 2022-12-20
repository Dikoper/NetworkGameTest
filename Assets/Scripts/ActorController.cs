using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ActorController : NetworkBehaviour
{
    [SerializeField] NavMeshAgent agent;
    [SerializeField] Camera back_camera;
    [SerializeField] float WALKSPEED = 5.0f;
    [SerializeField] float STRAFESPEED = 5.0f;
    [SerializeField] float DASHDISTANCE = 3.0f;
    [SerializeField] float dashCD = 1.0f;
    [SerializeField] float dizzyCD = 3.0f;

    public event System.Action<byte> OnPlayerNumberChanged;
    public event System.Action<ushort> OnPlayerDataChanged;

    public GameObject playerUIPrefab;
    public SceneScript scene;
    GameObject playerUIObject;
    PlayerUI playerUI = null;
    static readonly List<ActorController> playersList = new List<ActorController>();
    Material material;
    Vector3 rotation = Vector3.zero;

    public enum STATES 
    {
        NORMAL,
        DASHING,
        DIZZY
    }

    [SyncVar(hook = nameof(SetState))]
    public STATES state;

    [SyncVar(hook = nameof(PlayerNumberChanged))]
    public byte playerNumber = 0;

    [SyncVar(hook = nameof(PlayerDataChanged))]
    public ushort playerScore = 0;

    void PlayerNumberChanged(byte _, byte newPlayerNumber)
    {
        OnPlayerNumberChanged?.Invoke(newPlayerNumber);
    }

    void PlayerDataChanged(ushort _, ushort newPlayerData)
    {
        OnPlayerDataChanged?.Invoke(newPlayerData);
        CmdChangeScore(playerScore);
    }

    public void SetState(STATES oldValue, STATES newValue)
    {
        state = newValue;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        playersList.Add(this);
    }

    public override void OnStartClient()
    {
        // Instantiate the player UI as child of the Players Panel
        playerUIObject = Instantiate(playerUIPrefab, CanvasUI.GetPlayersPanel());
        playerUI = playerUIObject.GetComponent<PlayerUI>();

        // wire up all events to handlers in PlayerUI
        OnPlayerNumberChanged = playerUI.OnPlayerNumberChanged;
        OnPlayerDataChanged = playerUI.OnPlayerDataChanged;

        // Invoke all event handlers with the initial data from spawn payload
        OnPlayerNumberChanged.Invoke(playerNumber);
        OnPlayerDataChanged.Invoke(playerScore);
    }

    public override void OnStartLocalPlayer() 
    {
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        playerUI.SetLocalPlayer();
        CanvasUI.SetActive(true);

        agent = GetComponent<NavMeshAgent>();
        back_camera = Camera.main;
        back_camera.transform.SetParent(GetComponentInParent<Transform>());
        back_camera.GetComponent<CameraController>().target = transform;
    }

    void Awake()
    {
        scene = GameObject.FindObjectOfType<SceneScript>();
    }

    void Update()
    {
        ChangeColorByState();
        
        if (isLocalPlayer)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            agent.Move(vertical * WALKSPEED * back_camera.transform.forward * Time.deltaTime);
            agent.Move(horizontal * STRAFESPEED * back_camera.transform.right * Time.deltaTime);

            if (state != STATES.DASHING)
            {
                rotation = back_camera.transform.forward * vertical + back_camera.transform.right * horizontal;
                rotation.y = 0;

                agent.transform.LookAt(transform.position + rotation);

                if (Input.GetAxis("Fire1") > 0)
                {
                    StartCoroutine("DashRoutine");
                }
            }
        }
    }

    public override void OnStopLocalPlayer()
    {
        // Disable the main panel for local player
        CanvasUI.SetActive(false);
    }

    public override void OnStopClient()
    {
        OnPlayerNumberChanged = null;
        OnPlayerDataChanged = null;
        Destroy(playerUIObject);
    }
    public override void OnStopServer()
    {
        CancelInvoke();
        playersList.Remove(this);
    }

    private void OnDestroy()
    {
        Destroy(material);
    }

    void ChangeColorByState()
    {
        material = GetComponentInChildren<Renderer>().material;
        switch (state)
        {
            case STATES.NORMAL:
                material.color = Color.white;
                break;
            case STATES.DASHING:
                material.color = Color.green;
                break;
            case STATES.DIZZY:
                material.color = Color.red;
                break;
            default:
                break;
        }
    }

    [Server]
    public void ChangeState(STATES newValue) 
    {
        state = newValue;
    }

    [Server]
    public void ChangeScore(int value)
    {
        if (!scene.scoreTable.ContainsKey(playerNumber))
            scene.scoreTable.Add(playerNumber, value);
        else
            scene.scoreTable[playerNumber] = value;
    }

    [Command(requiresAuthority = false)]
    void CmdChangeScore(int value)
    {
        ChangeScore(value);
    }

    [Command(requiresAuthority = false)]
    public void CmdRespawn()
    {
        playerScore = 0;
    }

    [Command(requiresAuthority = false)]
    void CmdChangeState(STATES value)
    {
        ChangeState(value);
        RpcOnChangeState();
    }

    [ClientRpc]
    void RpcOnChangeState()
    {
        ChangeColorByState();
    }

    [ClientRpc]
    public void RpcRespawn()
    {
        transform.position = NetworkManager.startPositions[Random.Range(0, NetworkManager.startPositions.Count)].position;
        CmdRespawn();
    }

    private IEnumerator DashRoutine()
    {
        if (isServer)
            ChangeState(STATES.DASHING);
        else
            CmdChangeState(STATES.DASHING);

        Vector3 start = transform.position;

        agent.transform.LookAt(transform.position + back_camera.transform.forward);

        while ((transform.position - start).magnitude < DASHDISTANCE)
        {
            transform.position += back_camera.transform.forward * Mathf.Lerp(STRAFESPEED, STRAFESPEED * 5, 0.75f) * Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        yield return new WaitForSeconds(dashCD);
        if (isServer)
            ChangeState(STATES.NORMAL);
        else
            CmdChangeState(STATES.NORMAL);
    }

    private IEnumerator DizzyRoutine()
    {
        if (isServer)
            ChangeState(STATES.DIZZY);
        else
            CmdChangeState(STATES.DIZZY);

        yield return new WaitForSeconds(dizzyCD);

        if (isServer)
            ChangeState(STATES.NORMAL);
        else
            CmdChangeState(STATES.NORMAL);
    }

    [ServerCallback]
    private void OnTriggerEnter(Collider other)
    {
        var o = other.transform.GetComponent<ActorController>();
        if(o != null)
        {
            if (o.state == STATES.DASHING && state != STATES.DIZZY)
            {
                StartCoroutine("DizzyRoutine");
                o.playerScore++;
            }
        }
    }

    [ServerCallback]
    internal static void ResetPlayerNumbers()
    {
        byte playerNumber = 0;
        foreach (ActorController player in playersList)
        {
            player.playerNumber = playerNumber++;
        }
    }
}
