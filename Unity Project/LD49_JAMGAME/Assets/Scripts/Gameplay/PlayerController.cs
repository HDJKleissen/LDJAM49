using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public CharacterController CharacterController;

    // Vertical Physics
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private AnimationCurve jumpFallOff;
    [SerializeField] private float jumpMultiplier;
    [SerializeField] private float coyoteTime;

    bool canJump;
    bool previousGrounded = false;
    float velocityY = 0.0f;

    // Camera & Movement
    [SerializeField] Camera playerCamera = null;
    [SerializeField] float moveSpeed;
    [SerializeField] [Range(0.0f, 0.5f)] float moveSmoothTime;
    [SerializeField] [Range(0.0f, 0.5f)] float mouseSmoothTime;
    float cameraPitch = 0.0f;

    Vector2 currentDir = Vector2.zero;
    Vector2 currentDirVelocity = Vector2.zero;

    Vector2 currentMouseDelta = Vector2.zero;
    Vector2 currentMouseDeltaVelocity = Vector2.zero;

    // Pointing out bugs
    [SerializeField] LayerMask pointableLayer;
    [SerializeField] LayerMask interactableLayer;
    [SerializeField] float maxScanTime;
    [SerializeField] float interactRange;
    IFixable scanningBug = null;
    IHighlightable highlightedObject = null;
    float scanTime = 0;

    // Start is called before the first frame update
    void Start()
    {

    }


    void Update()
    {
        UpdateMouseLook();
        UpdateMovement();
        HandleJumping();
        HandlePointing();

        previousGrounded = CharacterController.isGrounded;
    }

    private void HandlePointing()
    {
        bool clearHighlight = true;
        RaycastHit hit;
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, interactRange, pointableLayer))
        {
            IHighlightable highlightAbleObject = hit.transform.GetComponent<IHighlightable>();
            if(highlightAbleObject == null)
            {
                highlightAbleObject = hit.transform.GetComponentInParent<IHighlightable>();
            }
            if (highlightAbleObject != null)
            {
                clearHighlight = false;

                if (highlightedObject == null || highlightedObject != highlightAbleObject)
                {
                    highlightedObject?.ToggleHighlight(false);
                    highlightedObject = highlightAbleObject;
                    highlightedObject.ToggleHighlight(true);
                }
            }

            if (Input.GetMouseButton(0))
            {
                IFixable bugHit = hit.transform.GetComponent<IFixable>();

                if (bugHit == null)
                {
                    bugHit = hit.transform.GetComponentInParent<IFixable>();
                }

                if (bugHit != null)
                {
                    if ((scanningBug == null || scanningBug != bugHit) && !bugHit.IsFixing && bugHit.IsBugged && !bugHit.IsFixed)
                    {
                        StartScan(bugHit);
                    }
                    else if (scanningBug == bugHit)
                    {
                        scanTime += Time.deltaTime;
                    }
                }
                else
                {
                    if (scanningBug != null)
                    {
                        StopScan();
                    }
                }
            }

            // Temporary Triggering stuff
            if (Input.GetKeyDown(KeyCode.T))
            {
                Bug bugHit = hit.transform.GetComponent<Bug>();

                if (bugHit == null)
                {
                    bugHit = hit.transform.GetComponentInParent<Bug>();
                }

                if (bugHit != null)
                {
                    if (!bugHit.IsBugged)
                    {
                        bugHit.StartBugging();
                    }
                }
            }
        }
        else
        {
            if (scanningBug != null)
            {
                StopScan();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            StopScan();
        }

        if (Physics.Raycast(ray, out hit, interactRange, interactableLayer))
        {
            Interactable interactableHit = hit.transform.GetComponent<Interactable>();

            if (interactableHit != null)
            {
                clearHighlight = false;

                if (highlightedObject == null || highlightedObject != interactableHit as IHighlightable)
                {
                    highlightedObject?.ToggleHighlight(false);
                    highlightedObject = interactableHit;
                    highlightedObject.ToggleHighlight(true);
                }
            }
            
            if (Input.GetButtonDown("Use"))
            {
                interactableHit.Interact();

            }
        }
        if (clearHighlight && highlightedObject != null)
        {
            highlightedObject.ToggleHighlight(false);
            highlightedObject = null;
        }    

        if (scanningBug != null)
        {
            GameManager.Instance.UpdateScanningUI(scanTime, maxScanTime);
            if (scanTime >= maxScanTime)
            {
                scanningBug.StartFix();
                StopScan();
            }
        }
    }



    void StartScan(IFixable bug)
    {
        Debug.Log("Starting scan");
        scanningBug = bug;
        scanTime = 0;
        maxScanTime = scanningBug.ScanTime;
    }

    void StopScan()
    {
        scanningBug = null;
        scanTime = 0;
        maxScanTime = -1;
        GameManager.Instance.DisableScanUI();
    }

    private void HandleJumping()
    {
        if (canJump)
        {
            if (Input.GetButton("Jump"))
            {
                canJump = false;
                StartCoroutine(JumpEvent());
            }
        }
        if (CharacterController.isGrounded && !canJump)
        {
            canJump = true;
        }
        if (!CharacterController.isGrounded && previousGrounded)
        {
            StartCoroutine(CoroutineHelper.DelaySeconds(() => canJump = false, coyoteTime));
        }
    }

    void UpdateMouseLook()
    {
        Vector2 targetMouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta, ref currentMouseDeltaVelocity, mouseSmoothTime);

        cameraPitch -= currentMouseDelta.y * GameManager.MouseSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -90.0f, 90.0f);

        playerCamera.transform.localEulerAngles = Vector3.right * cameraPitch;
        transform.Rotate(Vector3.up * currentMouseDelta.x * GameManager.MouseSensitivity);
    }

    void UpdateMovement()
    {
        Vector2 targetDir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        targetDir.Normalize();

        currentDir = Vector2.SmoothDamp(currentDir, targetDir, ref currentDirVelocity, moveSmoothTime);

        if (CharacterController.isGrounded)
            velocityY = -0.1f;

        velocityY += gravity * Time.deltaTime;

        float moveSpeedWithSprinting = moveSpeed;
        if (Input.GetButton("Sprint"))
        {
            moveSpeedWithSprinting *= 2;
        }

        Vector3 velocity = (transform.forward * currentDir.y + transform.right * currentDir.x) * moveSpeedWithSprinting + Vector3.up * velocityY;

        CharacterController.Move(velocity * Time.deltaTime);
    }
    private IEnumerator JumpEvent()
    {
        float timeInAir = 0.0f;
        do
        {
            float jumpForce = jumpFallOff.Evaluate(timeInAir);
            CharacterController.Move(Vector3.up * jumpForce * jumpMultiplier * Time.deltaTime);
            timeInAir += Time.deltaTime;

            yield return null;
        } while (!CharacterController.isGrounded && CharacterController.collisionFlags != CollisionFlags.Above);
    }
}