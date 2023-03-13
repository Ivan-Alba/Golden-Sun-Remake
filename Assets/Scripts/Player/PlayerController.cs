using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{

    [SerializeField] float speed = 4f;
    [SerializeField] float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;


    CharacterController controller;
    Animator playerAnim;
    Animator cloackAnim;
    AudioSource audioSource;
    [SerializeField] Transform mainCamera;
    GameObject objectHit;

    //-------------UI ELEMENTS---------------//
    //Stamina bar
    private GameObject iconCanvas;
    private GameObject staminaBar;
    private Slider staminaSlider;
    //Minimap
    GameObject minimap;
    //AIM
    GameObject aimImageUI;
    Camera aimCamera;
    bool aiming;
    //MainMenu
    GameObject mainMenu;

    //Character Info Bar
    GameObject partyInfoBar;
    bool partyInfoBarActive;

    /**
     * WEAPON HIT WORLD MAP
    [SerializeField] GameObject weaponObject;
    private BoxCollider weaponCollider;  
    int combo;
    */

    //LongTime idle animation
    float timeCount;
    float secondsInIdle = 15f;

    //StopPlayerController

    private bool stopPlayerController;
    public bool StopPlayerController { get => stopPlayerController; set => stopPlayerController = value; }

    private void Awake()
    {
        controller = gameObject.GetComponent<CharacterController>();
        playerAnim = gameObject.GetComponent<Animator>();
        //TODO SI EL PLAYER ES ISAAC
        cloackAnim = GameObject.Find("CloackIsaac").GetComponent<Animator>();

        audioSource = gameObject.GetComponent<AudioSource>();
        //weaponCollider = weaponObject.GetComponent<BoxCollider>();

        //Stamina bar
        iconCanvas = transform.Find("IconCanvas").gameObject;
        staminaBar = iconCanvas.transform.Find("StaminaBar").gameObject;
        staminaSlider = staminaBar.GetComponent<Slider>();

        //MainMenu
        mainMenu = GameObject.Find("MainMenu");
        mainMenu.SetActive(false);

        //Minimap
        minimap = transform.Find("PlayerCanvas").Find("Minimap").gameObject;

        //Aim
        aimCamera = transform.Find("Aim").Find("AimCamera").GetComponent<Camera>();
        aimImageUI = transform.Find("PlayerCanvas").transform.Find("Aim").gameObject;

        //PartyInfoBar
        partyInfoBar = transform.Find("PlayerCanvas").Find("PartyInfoBar").gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        if (StopPlayerController) //IF CONTROLER STOPPED
        {
            staminaBar.SetActive(false);
            return;
        }

        if (GameManager.instance.InBattle) //IF BATTLE DISABLE UPDATE
        {
            staminaBar.SetActive(false);
            if (partyInfoBarActive) {
                StartCoroutine(ShowPartyInfoBar(false));
            }
            return;
        }

        if (DialogueManager.instance.InDialogue) //IF DIALOG DISABLE UPDATE
        {
            staminaBar.SetActive(false);
            return;
        }

        //INTERACT 
        if (objectHit != null & objectHit.CompareTag("Interactable") &&
            Vector3.Distance(objectHit.transform.position, transform.position) < 2) //IF CLOSE AN INTERACTABLE OBJECT
        {
            //MOSTRAR BOTON O ICONO
            SetIconVisible(true);
            Debug.Log("INTERACTUAR");

            if (Input.GetKeyDown(GameManager.instance.KeyConfirm))
            {
                SetIconVisible(false);
                objectHit.GetComponent<Interactable>().Interact();
            }

        }
        else
        {
            SetIconVisible(false);
            if (Input.GetKeyDown(GameManager.instance.KeyConfirm))
            {
                //OpenWorldPsynergyPanel

            }
        }

        //----------------AIM----------------

        if (Input.GetKey(KeyCode.Q) && !aiming)
        {
            if (mainCamera.GetComponent<Camera>().enabled == true) Camera.main.enabled = false;
            aimCamera.enabled = true;
            aimImageUI.SetActive(true);
            transform.GetComponent<PlayerAiming>().enabled = true;
            aiming = true;
        }

        if (Input.GetKeyUp(KeyCode.Q) && aiming)
        {
            StopAiming();
        }


        //-------------MOVEMENT--------------

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(x, 0f, z).normalized;

        if (direction.magnitude >= 0.1f) //IF MOVING
        {
            timeCount = 0;

            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + mainCamera.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            playerAnim.SetBool("isMoving", true);
            cloackAnim.SetBool("isMoving", true);



            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            if (Input.GetKey(KeyCode.LeftShift)) //RUN PRESSED
            {
                playerAnim.SetBool("isRunning", true);
                cloackAnim.SetBool("isRunning", true);

                controller.Move(moveDirection.normalized * (speed * 2) * Time.deltaTime);

                //REFRESH STAMINA BAR
                RefreshStaminaBar(-2);


            }
            else //RUN NOT PRESSED
            {
                playerAnim.SetBool("isRunning", false);
                cloackAnim.SetBool("isRunning", false);

                controller.Move(moveDirection.normalized * speed * Time.deltaTime);

                //REFRESH STAMINA BAR
                RefreshStaminaBar(1);

            }
        }
        else //NOT MOVING
        {
            playerAnim.SetBool("isMoving", false);
            cloackAnim.SetBool("isMoving", false);

            //IDLE LONG TIME ANIMATION
            timeCount += Time.deltaTime;
            if (timeCount >= secondsInIdle)
            {
                playerAnim.SetTrigger("longTime");
                timeCount = 1;
            }

            //REFRESH STAMINA BAR
            RefreshStaminaBar(1);
        }


        //------------SHOW PARTY INFO BAR-----------//

        if (timeCount >= 1 && !aiming && !partyInfoBarActive)
        {
            RefreshPartyInfo();
            StartCoroutine(ShowPartyInfoBar(true));
        }


        if (timeCount < 1 && partyInfoBarActive)
        {
            StartCoroutine(ShowPartyInfoBar(false));
        }



        /**---------ATTACK------------
        if (Input.GetKeyDown(KeyCode.Q))
        {
            playerAnim.SetInteger("combo", combo++);
            playerAnim.SetTrigger("attack1");
        }*/


        //SHOW AND HIDE STAMINA BAR
        if (staminaSlider.value >= staminaSlider.maxValue)
        {
            ShowStaminaBar(false);
        }
        else
        {
            ShowStaminaBar(true);
        }


        //OPEN MAIN MENU
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (aiming)
            {
                StopAiming();
            }

            stopPlayerController = true;
            mainMenu.SetActive(true);
            minimap.transform.parent.gameObject.SetActive(false);
        }

    }

    private void StopAiming()
    {
        gameObject.GetComponent<PlayerAiming>().Reset();
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, transform.eulerAngles.z);
        mainCamera.GetComponent<Camera>().enabled = true;
        aimCamera.enabled = false;
        aimImageUI.SetActive(false);
        transform.GetComponent<PlayerAiming>().enabled = false;
        aiming = false;
    }
    private void FixedUpdate()
    {
        //RAYCAST
        var ray = new Ray(this.transform.position + new Vector3(0, 1, 0), this.transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            Debug.DrawRay(transform.position + new Vector3(0, 1, 0), transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);

            if (hit.transform.gameObject != null)
            {
                objectHit = hit.transform.gameObject;

            }
            else
            {
                objectHit = null;
            }
            //Debug.Log("RayHit = " + objectHit.name);
        }

    }

    private void RefreshPartyInfo()
    {
        for (int i = 0; i < GameManager.instance.GetParty().Count; i++)
        {
            partyInfoBar.transform.GetChild(i).transform.Find("Avatar").Find("Image").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/MainMenu/{GameManager.instance.GetParty()[i].name}Avatar");
            partyInfoBar.transform.GetChild(i).transform.Find("NamePanel").Find("Name").GetComponent<TextMeshProUGUI>().SetText(GameManager.instance.GetParty()[i].GetComponent<FighterStats>().Name);
            partyInfoBar.transform.GetChild(i).transform.Find("HP").GetComponent<TextMeshProUGUI>().SetText
                ($"{GameManager.instance.GetParty()[i].GetComponent<FighterStats>().CurrHp}/{GameManager.instance.GetParty()[i].GetComponent<FighterStats>().MaxHp}");
            partyInfoBar.transform.GetChild(i).transform.Find("PP").GetComponent<TextMeshProUGUI>().SetText
                              ($"{GameManager.instance.GetParty()[i].GetComponent<FighterStats>().CurrPp}/{GameManager.instance.GetParty()[i].GetComponent<FighterStats>().MaxPp}");
            partyInfoBar.transform.GetChild(i).transform.Find("Level").GetComponent<TextMeshProUGUI>().SetText($"Nivel: {GameManager.instance.GetParty()[i].GetComponent<FighterStats>().Level}");

            //Bars
            partyInfoBar.transform.GetChild(i).transform.Find("HPBar").GetComponent<Slider>().maxValue = GameManager.instance.GetParty()[i].GetComponent<FighterStats>().MaxHp;
            partyInfoBar.transform.GetChild(i).transform.Find("HPBar").GetComponent<Slider>().value = GameManager.instance.GetParty()[i].GetComponent<FighterStats>().CurrHp;

            partyInfoBar.transform.GetChild(i).transform.Find("PPBar").GetComponent<Slider>().maxValue = GameManager.instance.GetParty()[i].GetComponent<FighterStats>().MaxPp;
            partyInfoBar.transform.GetChild(i).transform.Find("PPBar").GetComponent<Slider>().value = GameManager.instance.GetParty()[i].GetComponent<FighterStats>().CurrPp;

            partyInfoBar.transform.GetChild(i).gameObject.SetActive(true);
        }
    }

    private IEnumerator ShowPartyInfoBar(bool choice)
    {
        if (choice)
        {
            partyInfoBar.SetActive(choice);
            partyInfoBarActive = true;
        }
        else
        {
            partyInfoBarActive = false;
            partyInfoBar.GetComponent<Animation>().Play("hide");

            while (partyInfoBar.GetComponent<Animation>().isPlaying)
            {
                yield return new WaitForSecondsRealtime(0.1f);
            }
            partyInfoBar.SetActive(false);
        }
    }

    public GameObject GameObjectForward()
    {
        return objectHit;
    }

    /**
    public void AttackFinished()
    {
        combo = 0;
        playerAnim.SetInteger("combo", combo);
    }
    **/

    /**
    public void PlaySwordSound()
    {

        AudioSource audioSource = weaponObject.GetComponent<AudioSource>();
        audioSource.Play();

    }
    **/

    public void SetIconVisible(bool active)
    {
        if (active)
        {
            iconCanvas.transform.Find("Icon").gameObject.SetActive(true);
        }
        else
        {
            iconCanvas.transform.Find("Icon").gameObject.SetActive(false);
        }
    }

    public void RefreshStaminaBar(float value)
    {
        staminaSlider.value += value;
    }

    private void ShowStaminaBar(bool value)
    {
        staminaBar.SetActive(value);
    }

    //Method called by walk/run animations
    public void playFootprints()
    {
        audioSource.Play();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Interactable"))
        {
            objectHit = other.gameObject;
        }
    }


}
