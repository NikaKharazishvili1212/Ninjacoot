using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static Utils;

public class MainMenu : MonoBehaviour
{
    [SerializeField] int menu = 1, selected = 1;
    [SerializeField] GameObject mainMenu, newGameMenu, difficultyMenu, tipsMenu;
    [SerializeField] Image background;
    [SerializeField] TextMeshProUGUI creatingAccountName;
    [SerializeField] TextMeshProUGUI[] menus, difficulties, letters;
    [SerializeField] Animator mainMenuAnim, newGameMenuAnim, tipsMenuAnim;
    [SerializeField] AudioClip selectionSound, submitSound, cancelSound, keyboardSound;
    [SerializeField] AudioSource audioSource;
    float selectionDelay;  // For menu selection delay

    // Loading
    [SerializeField] Image loadingBar;
    [SerializeField] TextMeshProUGUI loadingText;

    void Update() => MenuKeys();

    void MenuKeys()
    {
        selectionDelay = Mathf.Max(0, selectionDelay - Time.deltaTime);
        float verticalInput = Input.GetAxis("Vertical");
        if (menu == 0)  // Main menu(menus' selection)
        {
            if (verticalInput != 0 && selectionDelay == 0)
            {
                selectionDelay = 0.2f;
                ClearSelection("menus");

                selected = verticalInput > 0
                    ? (selected == 0 ? 3 : selected - 1)
                    : (selected == 3 ? 0 : selected + 1);

                SelectNext("menus");
                audioSource.PlayOneShot(selectionSound);
            }

            if (Input.GetButtonDown("Submit"))  // Select from Main Menu
            {
                mainMenuAnim.Play("MenuUp");
                if (selected == 0)  // New Game(account creation)
                {
                    newGameMenuAnim.Play("MenuUp2");
                    SelectNext("letters");
                }
                else if (selected == 1) { }  // Load
                else if (selected == 2) tipsMenuAnim.Play("MenuLeft");  // Tips
                else if (selected == 3) Exit();  // Exit game

                audioSource.PlayOneShot(submitSound);
                ClearSelection("menus");  // Selected text(yellow and bigger) becomes nonselected(white and normal size)
                menu = selected + 1;  // 1.Account Creation,   2.Load,   3.Tips,   4.Exit
                selected = 0;
                selectionDelay = 0.2f;
                background.color = Color.grey;
            }
        }
        //----------------------------------------------------------------------------------------
        else if (menu == 1)  // New Game(Account Creation)
        {
            if (Input.GetAxis("Horizontal") > 0 && selectionDelay == 0)  // Move to the right by 1 letter
            {
                selectionDelay = 0.2f;
                ClearSelection("letters");
                selected += 1;
                if (selected > letters.Length - 1) selected = 0;
                SelectNext("letters");
                audioSource.PlayOneShot(selectionSound);
            }
            else if (Input.GetAxis("Horizontal") < 0 && selectionDelay == 0)  // Move to the left by 1 letter
            {
                selectionDelay = 0.2f;
                ClearSelection("letters");
                selected -= 1;
                if (selected < 0) selected = letters.Length - 1;
                SelectNext("letters");
                audioSource.PlayOneShot(selectionSound);
            }
            else if (Input.GetAxis("Vertical") < 0 && selectionDelay == 0)  // Move down by 1 lane
            {
                selectionDelay = 0.2f;
                ClearSelection("letters");
                selected += 7;
                selected = selected == 28 ? 0 : selected == 29 ? 1 : selected == 30 ? 2 : selected == 31 ? 3 : selected == 32 ? 4 : selected == 33 ? 5 : selected == 34 ? 6 : selected;
                SelectNext("letters");
                audioSource.PlayOneShot(selectionSound);
            }
            else if (Input.GetAxis("Vertical") > 0 && selectionDelay == 0)  // Move up by 1 lane
            {
                selectionDelay = 0.2f;
                ClearSelection("letters");
                selected -= 7;
                selected = selected == -7 ? 21 : selected == -6 ? 22 : selected == -5 ? 23 : selected == -4 ? 24 : selected == -3 ? 25 : selected == -2 ? 26 : selected == -1 ? 27 : selected;
                SelectNext("letters");
                audioSource.PlayOneShot(selectionSound);
            }

            if (Input.GetButton("Submit") && selected == 26 && selectionDelay == 0)  // Delete one letter
            {
                selectionDelay = 0.2f;
                audioSource.PlayOneShot(cancelSound);
                creatingAccountName.text = creatingAccountName.text.Length > 0 ? creatingAccountName.text.Substring(0, creatingAccountName.text.Length - 1) : "";
            }
            else if (Input.GetButton("Submit") && selected == 27 && selectionDelay == 0)  // Create and save account by the chosen letters
            {
                selectionDelay = 0.2f;
                if (creatingAccountName.text.Length > 0)
                {
                    audioSource.PlayOneShot(submitSound);
                    ClearSelection("letters");
                    selected = 0;
                    SelectNext("difficulties");
                    menu = 11;
                    newGameMenuAnim.Play("MenuDown2");
                    this.Invoke2(0.2f, () => difficultyMenu.SetActive(true));
                }
                else audioSource.PlayOneShot(cancelSound);
            }
            else if (Input.GetButton("Submit") && selectionDelay == 0)  // Choose one letter
            {
                selectionDelay = 0.2f;
                audioSource.PlayOneShot(keyboardSound);
                creatingAccountName.text += letters[selected].text;
            }
        }
        else if (menu == 11)  // Choosing difficulty for our account
        {
            if (Input.GetAxis("Vertical") > 0 && selectionDelay == 0)
            {
                selectionDelay = 0.2f;
                ClearSelection("difficulties");
                selected = (selected - 1) < 0 ? 2 : selected - 1;
                SelectNext("difficulties");
                audioSource.PlayOneShot(selectionSound);
            }
            else if (Input.GetAxis("Vertical") < 0 && selectionDelay == 0)
            {
                selectionDelay = 0.2f;
                ClearSelection("difficulties");
                selected = (selected + 1) > 2 ? 0 : selected + 1;
                SelectNext("difficulties");
                audioSource.PlayOneShot(selectionSound);
            }
            else if (Input.GetButtonDown("Submit") && selectionDelay == 0)
            {
                selectionDelay = 0.2f;
                string difficulty = selected == 0 ? "Easy" : selected == 1 ? "Medium" : "Hard";
                PlayerPrefs.SetString("Account", creatingAccountName.text);
                PlayerPrefs.SetString(PlayerPrefs.GetString("Account") + "_Difficulty", difficulty);

                menu = 6; // Disable key of returning back to main menu because we are moving to new scene
                difficultyMenu.SetActive(false);
                audioSource.PlayOneShot(selectionSound);
                loadingBar.transform.parent.gameObject.SetActive(true);
                StartCoroutine(LoadSceneAsync("Level1", loadingBar, loadingText, 0.5f));
            }
        }
        //----------------------------------------------------------------------------------------
        if (menu != 0 && menu != 6 && Input.GetButtonDown("Cancel"))  // Return back to Main Menu
        {
            if (menu == 1)
            {
                ClearSelection("letters");
            }
            else if (menu == 11)
            {
                ClearSelection("difficulties");
            }
            background.color = Color.white;
            mainMenuAnim.Play("MenuDown");
            if (menu == 1) newGameMenuAnim.Play("MenuDown2");
            if (menu == 3) tipsMenuAnim.Play("MenuRight");
            menu = 0;
            selected = 0;
            SelectNext("menus");
            creatingAccountName.text = "";
            difficultyMenu.SetActive(false);
            audioSource.PlayOneShot(cancelSound);
        }
    }

    void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
               Application.Quit();
#endif
    }

    void ClearSelection(string str)
    {
        if (str == "menus")
        {
            menus[selected].color = Color.white;
            menus[selected].transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (str == "letters")
        {
            letters[selected].color = Color.white;
            letters[selected].transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (str == "difficulties")
        {
            difficulties[selected].color = Color.white;
            difficulties[selected].transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }

    void SelectNext(string str)
    {
        if (str == "menus")
        {
            menus[selected].color = Color.yellow;
            menus[selected].transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        }
        else if (str == "letters")
        {
            letters[selected].color = selected == 26 ? Color.red : selected == 27 ? Color.green : Color.yellow;
            letters[selected].transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        }
        else if (str == "difficulties")
        {
            difficulties[selected].color = Color.yellow;
            difficulties[selected].transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        }
    }
}