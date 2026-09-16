using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// PAINEL DE REGISTRO (LOG)

/// <summary>
/// Guarda e exibe as ultimas mensagens do jogo (sistema, item, ataque, dano, morte, revive).
///
/// Nao guarda referencia de ninguem: quem quiser registrar uma linha chama
/// Registrar(tipo, mensagem) diretamente (como LogBinder.cs faz, repassando
/// o que chega pelo GameLog.AoEscrever ou pelo EventManager).
///
/// Nao exige um Text no mesmo GameObject (o objeto do painel costuma ja ter
/// um Image de fundo, e o Unity so permite um componente Graphic por objeto).
/// Arraste o Text certo (geralmente um filho do painel) no campo "Texto" no
/// Inspector.
/// </summary>
public class ActionLog : MonoBehaviour
{
    public enum Tipo
    {
        Sistema,
        Item,
        Ataque,
        Dano,
        Morte,
        Revive
    }

    [SerializeField] private Text texto;
    [SerializeField] private int maximoDeLinhas = 8;

    private readonly List<string> linhas = new List<string>();

    private void Awake()
    {
        if (texto == null)
            Debug.LogWarning("ActionLog: arraste o Text do painel no campo 'Texto', no Inspector.", this);
    }

    // Adiciona uma linha ao log e atualiza o texto na tela.
    // Chamado por LogBinder.cs (via GameLog.AoEscrever) e diretamente onde precisar.
    public void Registrar(Tipo tipo, string mensagem)
    {
        string linha = $"[{Prefixo(tipo)}] {mensagem}";
        linhas.Add(linha);

        while (linhas.Count > maximoDeLinhas)
            linhas.RemoveAt(0);

        Atualizar();
    }

    // Limpa todas as linhas do log.
    public void Limpar()
    {
        linhas.Clear();
        Atualizar();
    }

    private void Atualizar()
    {
        if (texto == null) return;

        var construtor = new StringBuilder();
        for (int i = 0; i < linhas.Count; i++)
        {
            construtor.Append(linhas[i]);
            if (i < linhas.Count - 1) construtor.Append('\n');
        }

        texto.text = construtor.ToString();
    }

    private static string Prefixo(Tipo tipo)
    {
        switch (tipo)
        {
            case Tipo.Sistema: return "sistema";
            case Tipo.Item: return "item";
            case Tipo.Ataque: return "ataque";
            case Tipo.Dano: return "dano";
            case Tipo.Morte: return "morte";
            case Tipo.Revive: return "revive";
            default: return tipo.ToString();
        }
    }
}