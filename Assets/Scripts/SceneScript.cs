using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SceneScript : NetworkBehaviour
{
    public float restartCD = 5.0f;
    public int maxScore = 3;
    uint winner = 0;
    bool isRestart = false;

    public Text playerNameText;

    public SyncDictionary<uint, int> scoreTable = new SyncDictionary<uint, int>();

    void Start()
    {
        scoreTable.Callback += OnTableChange;
    }

    void OnTableChange(SyncDictionary<uint, int>.Operation op, uint key, int value)
    {
        switch (op)
        {
            case SyncIDictionary<uint, int>.Operation.OP_ADD:
                CheckScore(key);
                break;
            case SyncIDictionary<uint, int>.Operation.OP_SET:
                CheckScore(key);
                break;
            case SyncIDictionary<uint, int>.Operation.OP_REMOVE:
                // entry removed
                break;
            case SyncIDictionary<uint, int>.Operation.OP_CLEAR:
                // Dictionary was cleared
                break;
        }
    }
    void CheckScore(uint key)
    {
        if (scoreTable[key] >= maxScore)
        {
            winner = key;
            StartCoroutine(nameof(EndRoutine));
        }
    }

    IEnumerator EndRoutine() 
    {
        if(!isRestart)
        {
            playerNameText.transform.parent.gameObject.SetActive(true);
            playerNameText.text = string.Format("Player {0:00}", winner);

            isRestart = true;
            yield return new WaitForSeconds(restartCD);

            List<NetworkIdentity> slist = new List<NetworkIdentity>();
            foreach (var s in NetworkServer.spawned)
            {
                var playerScript = s.Value.gameObject.GetComponent<ActorController>();
                if (playerScript != null)
                    playerScript.RpcRespawn();
            }
            playerNameText.transform.parent.gameObject.SetActive(false);
            isRestart = false;
        }
    }
}
