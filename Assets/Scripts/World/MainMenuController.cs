using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Painéis de Menu")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;

    // O Start roda automaticamente assim que a cena é aberta no Play
    private void Start()
    {
        // Força o painel principal a ficar ligado e o de opções a ficar desligado no início
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    // Chamado pelo Botão JOGAR
    public void PlayGame()
    {
        SceneManager.LoadScene("Corrida");
    }

    // Chamado pelo Botão OPÇÕES
    public void OpenOptions()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    // Chamado pelo Botão VOLTAR (no menu de opções)
    public void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    // Chamado pelo Botão SAIR
    public void QuitGame()
    {
        Debug.Log("Saindo do jogo...");
        Application.Quit(); // Fecha o jogo (funciona na versão final compilada)
    }
}
