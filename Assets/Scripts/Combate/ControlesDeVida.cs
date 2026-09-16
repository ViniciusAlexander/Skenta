using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Health;

/// <summary>
/// Pocao de cura no teclado.
///
/// PROVISORIO POR DESENHO: a contagem de pocoes mora aqui so enquanto o modulo de
/// inventario nao existe. Quando existir, some tudo isto e o inventario passa a
/// chamar `jogador.UseItem("Potion")`, que ja funciona hoje. A cura em si nunca
/// foi responsabilidade daqui: quem cura e o modulo de vida.
/// </summary>
public class ControlesDeVida : MonoBehaviour
{
    [SerializeField, Range(1, 10)] private int limiteDePocoes = 5;

    public int Pocoes { get; private set; }
    public int Limite => limiteDePocoes;
    public event Action<int> PocoesMudaram;

    private Player jogador;

    private void Awake()
    {
        jogador = GetComponent<Player>();
        Pocoes = limiteDePocoes;
    }

    private void Start() => PocoesMudaram?.Invoke(Pocoes);

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame) Beber();
        if (Keyboard.current.rKey.wasPressedThisFrame) Reviver();
    }

    public void Beber()
    {
        if (jogador == null) return;

        if (jogador.EstaMorto)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "morto nao bebe pocao, use R");
            return;
        }

        if (Pocoes <= 0)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "sem pocoes");
            return;
        }

        if (jogador.VidaCheia)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "vida ja esta cheia");
            return;
        }

        Pocoes--;
        PocoesMudaram?.Invoke(Pocoes);

        // caminho que o inventario vai usar no lugar disto
        jogador.UseItem("Potion");

        GameLog.Escrever(ActionLog.Tipo.Item, $"bebeu pocao, restam {Pocoes}");
    }

    /// <summary>Provisorio tambem. Um jogo de verdade trata morte com tela ou checkpoint.</summary>
    public void Reviver()
    {
        if (jogador == null || !jogador.EstaMorto) return;
        jogador.ReviverNoPontoInicial();
        Pocoes = limiteDePocoes;
        PocoesMudaram?.Invoke(Pocoes);
    }
        // SISTEMA DE SALVAMENTO

    // Usado pelo PlayerDataManager (JSON ou PlayerPrefs) para repor a quantidade
    // de pocoes salva. Fica clampado entre 0 e o limite, do mesmo jeito que Beber()
    // ja respeita, para um save antigo ou editado na mao nao criar valor invalido.
    public void DefinirPocoes(int quantidade)
    {
        Pocoes = Mathf.Clamp(quantidade, 0, limiteDePocoes);
        PocoesMudaram?.Invoke(Pocoes);
    }
}
