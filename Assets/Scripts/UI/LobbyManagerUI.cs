using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManagerUI : NetworkBehaviour
{
    [SerializeField] private Button _hostBtn;
    [SerializeField] private Button _clientBtn;
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _waitingTxt;
    [SerializeField] private GameObject _crosshair;
    [SerializeField] private GameObject _UIContainer;

    private void Awake()
    {
        _hostBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
            GameManager.Instance.ClientType = EClientType.HOST;
            _startButton.gameObject.SetActive(true);
            _clientBtn.gameObject.SetActive(false);
            _hostBtn.gameObject.SetActive(false);
            _waitingTxt.gameObject.SetActive(true);
        });

        _clientBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
            GameManager.Instance.ClientType = EClientType.CLIENT;
            _clientBtn.gameObject.SetActive(false);
            _hostBtn.gameObject.SetActive(false);
            _waitingTxt.gameObject.SetActive(true);
        });

        _startButton.onClick.AddListener(() =>
        {
            HideLobbyUIClientRpc();
        });
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [ClientRpc]
    public void HideLobbyUIClientRpc()
    {
        this.gameObject.SetActive(false);
        GameManager.Instance.IsStarted = true;
        Cursor.lockState = CursorLockMode.Locked;
        _crosshair.SetActive(true);
        _UIContainer.SetActive(true);
        Cursor.visible = false;
    }
}
