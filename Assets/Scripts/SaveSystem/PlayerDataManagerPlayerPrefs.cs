using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// SISTEMA DE SALVAMENTO (PlayerPrefs)

/// <summary>
/// Mesma ideia do PlayerDataManager (JSON), mas guardando os dados no PlayerPrefs
/// em vez de um arquivo. No Windows isto vai para o Registro, em
/// HKEY_CURRENT_USER\Software\<empresa>\<jogo> (ver Parte 5.2 do guia).
///
/// A interface publica e identica de proposito: SalvarJogo() e CarregarJogo(), os
/// mesmos nomes que a versao JSON usa. Assim os dois componentes podem ficar lado a
/// lado no mesmo GameObject, cada um com seu proprio On Click () de botao, e comparar
/// as duas formas ao vivo para o professor (Parte 4 do guia explica como alternar
/// entre elas sem as duas reagirem juntas ao mesmo F5/F9).
///
/// Inventario: PlayerPrefs so guarda numero e string soltos, nao uma lista. Entao
/// salvamos a quantidade de slots ocupados numa chave (Save_ItemCount) e, para cada
/// slot, duas chaves numeradas: Save_Item_0_Nome / Save_Item_0_Qtd, Save_Item_1_Nome /
/// Save_Item_1_Qtd, e assim por diante.
/// </summary>
public class PlayerDataManagerPlayerPrefs : MonoBehaviour
{
    private const string ChaveExiste = "Save_Existe";
    private const string ChavePosX = "Save_PosX";
    private const string ChavePosY = "Save_PosY";
    private const string ChaveVida = "Save_VidaAtual";
    private const string ChavePocoes = "Save_Pocoes";
    private const string ChaveItemCount = "Save_ItemCount";

    private void OnEnable()
    {
        EventManager.OnSaveRequested += SalvarJogo;
        EventManager.OnLoadRequested += CarregarJogo;
    }

    private void OnDisable()
    {
        EventManager.OnSaveRequested -= SalvarJogo;
        EventManager.OnLoadRequested -= CarregarJogo;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.f5Key.wasPressedThisFrame) SalvarJogo();
        if (Keyboard.current.f9Key.wasPressedThisFrame) CarregarJogo();
    }

    // Salva posicao, vida, pocoes e inventario do jogador no PlayerPrefs.
    // Chamada por F5, pelo EventManager ou por um botao de UI (On Click > SalvarJogo()).
    public void SalvarJogo()
    {
        var jogador = FindFirstObjectByType<Player>();

        if (jogador == null)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "salvar (PlayerPrefs) falhou: jogador nao encontrado");
            return;
        }

        var controles = jogador.GetComponent<ControlesDeVida>();
        var inventario = FindFirstObjectByType<Inventory>();

        PlayerPrefs.SetFloat(ChavePosX, jogador.transform.position.x);
        PlayerPrefs.SetFloat(ChavePosY, jogador.transform.position.y);
        PlayerPrefs.SetInt(ChaveVida, jogador.GetCurrentHealth());
        PlayerPrefs.SetInt(ChavePocoes, controles != null ? controles.Pocoes : 0);

        // Apaga as chaves de itens do save anterior antes de escrever as novas,
        // senao um save com menos itens deixaria sobras do save maior de antes.
        int quantidadeAntiga = PlayerPrefs.GetInt(ChaveItemCount, 0);
        for (int i = 0; i < quantidadeAntiga; i++)
        {
            PlayerPrefs.DeleteKey(ChaveItemNome(i));
            PlayerPrefs.DeleteKey(ChaveItemQtd(i));
        }

        int quantidadeNova = 0;
        if (inventario != null)
        {
            foreach (InventorySlot slot in inventario.slots)
            {
                if (slot.item == null) continue;

                PlayerPrefs.SetString(ChaveItemNome(quantidadeNova), slot.item.itemName);
                PlayerPrefs.SetInt(ChaveItemQtd(quantidadeNova), slot.quantity);
                quantidadeNova++;
            }
        }

        PlayerPrefs.SetInt(ChaveItemCount, quantidadeNova);
        PlayerPrefs.SetInt(ChaveExiste, 1);
        PlayerPrefs.Save();

        GameLog.Escrever(ActionLog.Tipo.Sistema, "jogo salvo via PlayerPrefs");
    }

    // Le o PlayerPrefs e aplica posicao, vida, pocoes e inventario ao jogador da cena.
    // Chamada por F9, pelo EventManager ou por um botao de UI (On Click > CarregarJogo()).
    public void CarregarJogo()
    {
        if (PlayerPrefs.GetInt(ChaveExiste, 0) == 0)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "nenhum save (PlayerPrefs) encontrado");
            return;
        }

        var jogador = FindFirstObjectByType<Player>();

        if (jogador == null)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "carregar (PlayerPrefs) falhou: jogador nao encontrado");
            return;
        }

        float posX = PlayerPrefs.GetFloat(ChavePosX, jogador.transform.position.x);
        float posY = PlayerPrefs.GetFloat(ChavePosY, jogador.transform.position.y);
        int vidaSalva = PlayerPrefs.GetInt(ChaveVida, jogador.GetCurrentHealth());
        int pocoesSalvas = PlayerPrefs.GetInt(ChavePocoes, 0);

        jogador.transform.position = new Vector3(posX, posY, jogador.transform.position.z);

        // LoadHealth ja dispara o evento de mudanca de vida por dentro do modulo
        // Game.Health, entao o HUD se atualiza sozinho, sem precisar avisar ninguem aqui.
        jogador.LoadHealth(vidaSalva);

        var controles = jogador.GetComponent<ControlesDeVida>();
        if (controles != null) controles.DefinirPocoes(pocoesSalvas);

        var inventario = FindFirstObjectByType<Inventory>();
        if (inventario != null)
        {
            // Zera o inventario atual antes de recolocar o que veio do save,
            // senao os itens pegos depois do save ficariam somados aos salvos.
            inventario.slots.Clear();

            int quantidade = PlayerPrefs.GetInt(ChaveItemCount, 0);
            for (int i = 0; i < quantidade; i++)
            {
                string nome = PlayerPrefs.GetString(ChaveItemNome(i), null);
                int qtd = PlayerPrefs.GetInt(ChaveItemQtd(i), 0);

                if (string.IsNullOrEmpty(nome) || qtd <= 0) continue;

                ItemData item = inventario.itemDatabase != null
                    ? inventario.itemDatabase.GetItemByName(nome)
                    : null;

                if (item != null)
                {
                    inventario.AddItem(item, qtd);
                }
            }

            // Garante que a UI se atualize mesmo se nenhum item tiver sido
            // adicionado de volta (save antigo sem itens, ou nomes que nao
            // batem mais com a ItemDatabase) - senao os icones antigos ficariam
            // na tela ate o proximo item ser coletado.
            ForcarAtualizacaoDaUI(inventario);
        }

        GameLog.Escrever(ActionLog.Tipo.Sistema, "jogo carregado via PlayerPrefs");
    }

    // Monta a chave "Save_Item_{i}_Nome" para o slot de indice i.
    private static string ChaveItemNome(int indice) => $"Save_Item_{indice}_Nome";

    // Monta a chave "Save_Item_{i}_Qtd" para o slot de indice i.
    private static string ChaveItemQtd(int indice) => $"Save_Item_{indice}_Qtd";

    // Forca o evento Inventory.OnInventoryChanged a disparar, sem precisar
    // mexer em Inventory.cs. Em C#, um "event" so pode ser invocado de dentro
    // da classe onde foi declarado - por baixo dos panos ele vira um campo
    // privado com o mesmo nome, guardando os metodos inscritos (aqui, o
    // UpdateSlots do InventoryUI). Aqui a gente usa reflection pra pegar esse
    // campo escondido e chamar ele na mao.
    //
    // Necessario porque Clear() + AddItem() so avisa a UI quando algo e
    // realmente adicionado; se o save nao tiver itens (ou nenhum nome bater
    // com a ItemDatabase), nenhum AddItem roda, nenhum evento dispara, e a UI
    // ficaria mostrando os icones antigos ate o jogador pegar outro item.
    private static void ForcarAtualizacaoDaUI(Inventory inventario)
    {
        if (inventario == null) return;

        FieldInfo campoDoEvento = typeof(Inventory).GetField(
            "OnInventoryChanged",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (campoDoEvento == null) return;

        var inscritos = campoDoEvento.GetValue(inventario) as Action<List<InventorySlot>>;
        inscritos?.Invoke(inventario.slots);
    }
}