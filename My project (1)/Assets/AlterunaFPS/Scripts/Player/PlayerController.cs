using System.Linq;
using UnityEngine;
using Alteruna;
using Alteruna.Trinity;
// Assuming KeyMouse.MoHide is required for the HidingCharacter logic. 
// If not, remove this using line and the HidingCharacter variables.
using KeyMouse.MoHide; 

namespace AlterunaFPS
{
    public partial class PlayerController : Synchronizable
    {
        [Header("References")]
       
        [SerializeField] private Rigidbody _rb;
        
        // Renamed to BodyAnimator to distinguish from GunAnimator
        [SerializeField] private Animator _bodyAnimator; 
        

        // If you are using Alteruna, you often need a reference to the Multiplayer component
        private Multiplayer _multiplayer;
        [Header("Gun")]
        public Transform GunRoot;
        public Transform FirePoint;
        public IKControl IKController;
        public Animator GunAnimator;
        public LayerMask BulletCollisionLayers = ~0;
        public int GunMagazineSize = 5;
        public float GunFireTime = 0.2f;
        public float GunReloadTime = 2.35f;
        public float DistanceFromBody = 0.3f;
        
        [Header("Aiming")]
        public float ZoomFov = 30f;
        public float ZoomInTime = 0.2f;
        public float ZoomOutTime = 0.18f;

        [Header("Movement Stats")]
        [SerializeField] private float moveSpeed = 5;
        [SerializeField] private float rotationSpeed = 10;
        [SerializeField] private float jumpForce = 10;
        
        [Header("Health & Death")]
        public float Health = 100;
        [SerializeField] private HidingCharacter hidingCharacter; // From Script B
        [SerializeField] private GameObject DeathEffect;
        [SerializeField] private float DeathTimeScaler = 0.2f;
        [SerializeField] private AudioSource PlayerAudioSource;
        [SerializeField] private AudioClip DeathClip;

        // Internal State Variables
        private float _gunFireCooldown;
        private float _gunReloadCooldown;
        private int _gunMagazine;
        private float _gunBaseHeight;
        private Transform _gunLooker;
        
        // Movement State
        private bool _onGround;
        private bool _isOwner = true;
        private const string MOVE_AMOUNT_ANIMATION_VARIABLE = "Move amount";
        private const string JUMP_ANIMATION_VARIABLE = "Jump";
        
        // Alteruna/Multiplayer placeholder properties 
        // (Assuming these exist in the other partial part of the class, 
        // but defined here privately just in case they are missing)
         // Should be set by Alteruna Attributes
        private bool _offline = false;
        
        

        private void Start()
        {
            // Initialize Components
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_bodyAnimator == null) _bodyAnimator = GetComponent<Animator>();
            if (_cameraTarget == null) _cameraTarget = Camera.main.transform;

            InitializeGun();
            ResetAmmo();
            
            // Alteruna specific check (Pseudo-code if not using the full Alteruna package)
            var user = Multiplayer.GetUser();
            if (user == Multiplayer.Instance.Me);
        }

        private void Update()
        {
            // IMPORTANT: Only process Input for the local player
            if (!_isOwner) return;

            GunAction();
            HandleJump();
            CheckIfDead();
        }

        private void FixedUpdate()
        {
            if (!_isOwner) return;

            HandleMovement();
        }

        #region Gun Logic (From Script A)

        private void InitializeGun()
        {
            if (GunRoot == null) return;

            _gunBaseHeight = GunRoot.localPosition.y;

            // create a new object to help manage the gun rotation
            _gunLooker = new GameObject("GunLooker").transform;
            _gunLooker.SetParent(GunRoot);
            _gunLooker.position = GunRoot.position;
        }

        private void ResetAmmo()
        {
            _gunMagazine = GunMagazineSize;
        }

        private void GunAction(bool lockInput = false)
        {
            if (Input.GetKey(KeyCode.Mouse1))
            {
                CinemachineVirtualCameraInstance.Instance.SetFov(ZoomFov, ZoomInTime);
            }
            else
            {
                CinemachineVirtualCameraInstance.Instance.ResetFov(ZoomOutTime);
            }

            if (GunAnimator != null)
            {
                if (_firstPerson || Quaternion.Angle(_cameraTarget.rotation, GunRoot.parent.rotation) < 50)
                {
                    var camForward = _cameraTarget.forward;

                    // move the gun to follow the camera
                    // Check parent to avoid null ref if GunRoot has no parent
                    float parentY = GunRoot.parent != null ? GunRoot.parent.eulerAngles.y : transform.eulerAngles.y;
                    var rad = GetAngleBetweenAngles(_cameraTarget.eulerAngles.y, parentY) * Mathf.Deg2Rad;
                    
                    GunRoot.localPosition = new Vector3(Mathf.Sin(rad / 2f) * DistanceFromBody, _gunBaseHeight + camForward.y * DistanceFromBody * 0.95f, Mathf.Cos(rad) * DistanceFromBody);

                    // point that the gun is aiming at
                    Vector3 point;
                    
                    // aim gun at camera target
                    if (Physics.Raycast(_cameraTarget.position + camForward, camForward, out var hit, 50f) && hit.collider.gameObject != gameObject)
                    {
                        point = hit.point;
                    }
                    else
                    {
                        // if nothing is hit, aim at a point far away
                        point = camForward * 100 + _cameraTarget.position;
                    }

                    // rotate the gun to look at the point with some smoothing
                    _gunLooker.LookAt(point);
                    GunRoot.rotation = Quaternion.Lerp(GunRoot.rotation, _gunLooker.rotation, Time.deltaTime * 10);
                }

                if(IKController != null) IKController.IkActive = true;

                // if the gun is firing or reloading, don't allow any other actions
                if ((_gunFireCooldown -= Time.deltaTime) > 0 || (_gunReloadCooldown -= Time.deltaTime) > 0)
                {
                    return;
                }

                // Only allow sending the fire command if the player is owner.
                if (_isOwner)
                {
                    if (Input.GetKeyDown(KeyCode.Mouse0) && _gunMagazine > 0)
                    {
                        if (_offline)
                            FireBullet(0, FirePoint.position, FirePoint.forward);
                        else
                            BroadcastRemoteMethod(nameof(FireBullet), Multiplayer.GetUser().Index, FirePoint.position, FirePoint.forward, 10f, 10f);
                        
                        return;
                    }
                }

                if (Input.GetKeyDown(KeyCode.R) && (_gunMagazine < GunMagazineSize)) // Added 'R' key check for reload
                {
                    GunAnimator.Play(_animIDGunReload);
                    _gunReloadCooldown = GunReloadTime;
                    _gunMagazine = GunMagazineSize;
                }
                else if (_gunMagazine <= 0)
                {
                     // Auto reload on empty?
                     GunAnimator.Play(_animIDGunReload);
                    _gunReloadCooldown = GunReloadTime;
                    _gunMagazine = GunMagazineSize;
                }
            }
            else
            {
               if(IKController != null) IKController.IkActive = false;
            }
        }

        
        private void FireBullet(ushort senderID, Vector3 origin, Vector3 direction, float penetration = 10f, float damage = 10f)
        {
            if(GunAnimator) GunAnimator.Play(_animIDGunFire);

            _gunFireCooldown = GunFireTime;
            _gunMagazine--;

            float currentPenetration = penetration;
            float hitDistance = 40f;

            // Raycast with penetration
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, hitDistance, BulletCollisionLayers);
            // Sort the hits by distance
            hits = hits.OrderBy(h => h.distance).ToArray();
            int l = hits.Length;

            for (int i = 0; i < l; i++)
            {
                if (hits[i].collider.TryGetComponent(out Health target))
                {
                    EfxManager.Instance.PlayImpact(hits[i].point, hits[i].normal, hits[i].transform, target.MaterialType);

                    float distanceDamageDropoff = 10f / (hits[i].distance + 10f);
                    if ((currentPenetration - target.PenetrationResistance) / penetration * distanceDamageDropoff <= 0)
                    {
                        DrawLine(i, Color.red);
                        // fragmentation damage
                        target.TakeDamage(senderID, Mathf.Min(2 * currentPenetration / penetration, 1f) * damage * distanceDamageDropoff);
                        hitDistance = hits[i].distance;

                        // If penetration is not enough to go through the target, stop the bullet
                        break;
                    }

                    // penetration damage with dropoff
                    target.TakeDamage(senderID, currentPenetration / penetration * damage * distanceDamageDropoff);
                    DrawLine(i, Color.yellow);

                    // decreases its penetration after the projectile have exited the target
                    currentPenetration -= target.PenetrationResistance;
                }
                else
                {
                    if (hits[i].collider.gameObject.layer == 0) // default layer
                    {
                        EfxManager.Instance.PlayImpact(hits[i].point, hits[i].normal, hits[i].transform);
                        currentPenetration -= 5f;

                        DrawLine(i, Color.grey);
                    }
                    else
                        DrawLine(i, Color.black);
                }
            }

            EfxManager.Instance.PlayBullet(origin, direction, hitDistance / 100f);

            void DrawLine(int i, Color color, float duration = 1f)
            {
                if (i == 0)
                    Debug.DrawLine(origin, hits[i].point, color, duration);
                else
                    Debug.DrawLine(hits[i - 1].point, hits[i].point, color, duration);
            }
        }
        
        // Helper math from Script A
        

        #endregion

        #region Movement Logic (From Script B)

        private void HandleMovement()
        {
            float vertical = Input.GetAxis("Vertical");
            float horizontal = Input.GetAxis("Horizontal");

            float moveAmount = Mathf.Clamp01(Mathf.Abs(vertical) + Mathf.Abs(horizontal));
            
            // Using _cameraTarget instead of 'camera'
            Vector3 forwardLook = new Vector3(_cameraTarget.forward.x, 0, _cameraTarget.forward.z);
            Vector3 moveDirection = forwardLook * vertical + _cameraTarget.right * horizontal;

            ApplyVelocity(moveDirection);

            // Animation
            if (_bodyAnimator != null)
            {
                _bodyAnimator.SetFloat(MOVE_AMOUNT_ANIMATION_VARIABLE, moveAmount);
            }

            // Rotation
            // Note: In standard FPS, you might want the body to always rotate with camera, 
            // but this keeps the logic from Script B (rotate towards movement).
            moveDirection += _cameraTarget.right * horizontal;
            RotationNormal(moveDirection);
        }

        private void ApplyVelocity(Vector3 moveDirection)
        {
            Vector3 velocityDir = moveDirection * moveSpeed;

            // NOTE: 'linearVelocity' is for Unity 6+. 
            // If you are on an older version, change this to 'rb.velocity'.
            velocityDir.y = _rb.linearVelocity.y;
            _rb.linearVelocity = velocityDir;
        }

        private void RotationNormal(Vector3 rotationDirection)
        {
            Vector3 targetDir = rotationDirection;
            targetDir.y = 0;
            if (targetDir == Vector3.zero)
                targetDir = transform.forward;
            
            Quaternion lookDir = Quaternion.LookRotation(targetDir);
            Quaternion targetRot = Quaternion.Slerp(transform.rotation, lookDir, rotationSpeed * Time.deltaTime);
            transform.rotation = targetRot;
        }

        private void HandleJump()
        {
            if (!_onGround) return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }

        #endregion

        #region Health & Checks (From Script B)

        public void CheckIfDead()
        {
            if (Health <= 0)
            {
                // Disable player visual if using hiding character logic
                if (hidingCharacter != null && hidingCharacter.currentObject != null)
                {
                    hidingCharacter.currentObject.gameObject.SetActive(false);
                    hidingCharacter.BlockTransformation = true;
                    
                     // Instantiate particle death effect
                    if(DeathEffect)
                        Destroy(Instantiate(DeathEffect, hidingCharacter.currentObject.transform.position, Quaternion.Euler(-90, 0, 0)), 2);
                }

                // Play audio clip
                if (PlayerAudioSource != null && DeathClip != null)
                {
                    PlayerAudioSource.clip = DeathClip;
                    PlayerAudioSource.Play();
                }

                // Set time to slow down
                Time.timeScale = DeathTimeScaler;

                // Disable this script
                this.enabled = false;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            SetJumpState(true);
        }

        private void OnCollisionExit(Collision collision)
        {
            SetJumpState(false);
        }

        private void SetJumpState(bool onGround)
        {
            _onGround = onGround;
            if (_bodyAnimator != null)
            {
                _bodyAnimator.SetBool(JUMP_ANIMATION_VARIABLE, !onGround);
            }
        }
        public override void AssembleData(Writer writer, byte LOD = 100)
            {
        // If you need to sync variables manually, you write them here.
        // For now, we leave it empty to fix the error.
        // writer.Write(SomeVariable);
            }

        public override void DisassembleData(Reader reader, byte LOD = 100)
            {
        // If you wrote variables above, you read them here in the same order.
        // reader.ReadInt();
            }
        #endregion
    }
    
    
}   