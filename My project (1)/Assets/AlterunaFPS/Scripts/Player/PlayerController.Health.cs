using System;
using UnityEngine;
using Random = UnityEngine.Random;
using Alteruna; // FIX 1: Added this to find the 'Multiplayer' class

namespace AlterunaFPS
{
    public partial class PlayerController
    {
        [Header("Health")]
        public float MaxHealth = 20f;
        
        private Health _health;
        private int _lastSpawnIndex;

        // FIX 2: Added these placeholders. 
        // Ideally, these should be defined in your MAIN PlayerController.cs file, not here.
        // If they are missing there, you must add them.
        private bool _possesed; 
        private bool _isHost;   
        
        
        private void InitializeHealth()
        {
            _health = GetComponent<Health>();
            
            // _isOwner is inherited from Synchronizable or defined in the main file
            if (_isOwner) 
            {
                _health.OnDeath.AddListener(OnDeath);
                _health.HealthPoints = MaxHealth;
            }
        }

        private void OnDeath(ushort senderID)
        {
            if (_possesed)
            {
                // Ensure CinemachineVirtualCameraInstance exists or remove this line if not used
                // CinemachineVirtualCameraInstance.Instance.gameObject.SetActive(false);
                // CinemachineVirtualCameraInstance.Instance.Follow(null);

                // Ensure ScoreBoard exists
                if (ScoreBoard.Instance != null)
                {
                    ScoreBoard.Instance.AddDeaths(Avatar.Possessor, 1);
                    ScoreBoard.Instance.AddKills(senderID, 1);
                }

                if (_isHost)
                {
                    // Logic for host
                }
            }
            
            _health.HealthPoints = MaxHealth;

            if (_offline)
            {
                transform.position = Vector3.zero;
            }
            else
            {
                // FIX 3: Multiplayer is usually an instance, not static. 
                // We use 'Multiplayer.Instance' if it is a singleton, 
                // otherwise you need a reference like '_multiplayer'.
                var mp = Multiplayer.Instance; 
                
                if (mp == null) return; // Safety check

                int spawnIndex = 0;
                int spawnLocationsCount = mp.AvatarSpawnLocations.Count;

                if (spawnLocationsCount > 1)
                {
                    do
                    {
                        spawnIndex = Random.Range(0, spawnLocationsCount);
                    }
                    while (_lastSpawnIndex == spawnIndex);
                }
                else if (spawnLocationsCount <= 0)
                {
                    // Changed to simple Debug.LogError to prevent crashing the game flow
                    Debug.LogError("AvatarSpawnLocations must be greater than zero.");
                    return;
                }

                Transform spawn = mp.AvatarSpawnLocations.Count > 0 ? 
                    mp.AvatarSpawnLocations[spawnIndex] : 
                    mp.AvatarSpawnLocation;
                
                // Assuming _controller is defined in main PlayerController.cs
                if (_controller != null) _controller.enabled = false;
                
                transform.position = spawn.position;
                transform.rotation = spawn.rotation;
                
                // Assuming these are defined in main file
                _cinemachineTargetYaw = _bodyRotate = spawn.rotation.y;
                
                if (_controller != null) _controller.enabled = true;
            }
            
            // Ensure RespawnController exists
            // RespawnController.Respawn(gameObject);
        }
    }
}