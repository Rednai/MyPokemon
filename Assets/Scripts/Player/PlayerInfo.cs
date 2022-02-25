using Mirror;
using UnityEngine;
using TMPro;

namespace MyPokemon
{
    public class PlayerInfo : NetworkBehaviour
    {
        public GameObject floatingInfo;
        public TextMeshPro playerNameText;

        [SyncVar(hook = nameof(OnNameChanged))]
        public string playerName;

        public override void OnStartLocalPlayer()
        {
            string name = "Player" + Random.Range(100, 999);
            CmdSetupPlayerInfo(name);

            // we hide the floating info for the local player
            floatingInfo.SetActive(false);
        }

        [Command]
        public void CmdSetupPlayerInfo(string name)
        {
            playerName = name;
        }

        void OnNameChanged(string _Old, string _New)
        {
            // update the player name in the floating info
            playerNameText.text = playerName;
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                floatingInfo.transform.LookAt(Camera.main.transform);
                return;
            }
        }
    }
}
