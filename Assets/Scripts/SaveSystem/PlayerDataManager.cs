using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// SISTEMA DE SALVAMENTO (JSON)

/// <summary>
/// Salva e carrega o progresso do jogador num arquivo savegame.json,
/// dentro de Application.persistentDataPath.
///
/// Nao inventa nada novo no resto do projeto: usa Player.GetCurrentHealth()/LoadHealth(),
/// que ja existiam prontos para isto, ControlesDeVida.Pocoes / DefinirPocoes() para as
/// pocoes, e Inventory.slots / AddItem() / itemDatabase para o inventario.
/// Reage a tres gatilhos, todos chamando os mesmos dois metodos publicos:
///   - teclado (F5 salva, F9 carrega), igual ao resto do projeto usa Keyboard.current;
///   - EventManager.OnSaveRequested / OnLoadRequested, o barramento que ja existia
///     no projeto sem ninguem disparando ou escutando ainda;
///   - botao de UI, via On Click () > PlayerDataManager > SalvarJogo()/CarregarJogo().
///
/// Versao irma: PlayerDataManagerPlayerPrefs.cs faz a mesma coisa com PlayerPrefs em vez
/// de arquivo. Podem conviver no mesmo GameObject para comparar as duas ao vivo (ver o
/// guia passo a passo, Parte 4).
/// </summary>
public class PlayerDataManager : MonoBehaviour
{
    // Um item salvo: so o nome (para achar na ItemDatabase) e a quantidade.
    // Nao da para salvar o ItemData (ScriptableObject) direto em JSON.
    [Serializable]
    private class ItemSalvo
    {
        public string nome;
        public int quantidade;
    }

    [Serializable]
    private class SaveData
    {
        public float posX;
        public float posY;
        public int vidaAtual;
        public int pocoes;
        public List<ItemSalvo> itens;
    }

    private string CaminhoDoArquivo => Path.Combine(Application.persistentDataPath, "savegame.json");

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

    // Salva posicao, vida, pocoes e inventario do jogador em disco.
    // Chamada por F5, pelo EventManager ou por um botao de UI (On Click > SalvarJogo()).
    public void SalvarJogo()
    {
        var jogador = FindFirstObjectByType<Player>();

        if (jogador == null)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "salvar (JSON) falhou: jogador nao encontrado");
            return;
        }

        var controles = jogador.GetComponent<ControlesDeVida>();
        var inventario = FindFirstObjectByType<Inventory>();

        var itens = new List<ItemSalvo>();
        if (inventario != null)
        {
            foreach (InventorySlot slot in inventario.slots)
            {
                if (slot.item == null) continue;

                itens.Add(new ItemSalvo
                {
                    nome = slot.item.itemName,
                    quantidade = slot.quantity
                });
            }
        }

        var data = new SaveData
        {
            posX = jogador.transform.position.x,
            posY = jogador.transform.position.y,
            vidaAtual = jogador.GetCurrentHealth(),
            pocoes = controles != null ? controles.Pocoes : 0,
            itens = itens
        };

        try
        {
            File.WriteAllText(CaminhoDoArquivo, JsonUtility.ToJson(data, true));
            GameLog.Escrever(ActionLog.Tipo.Sistema, "jogo salvo via JSON");
        }
        catch (Exception e)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, $"salvar (JSON) falhou: {e.Message}");
        }
    }

    // Le o savegame.json e aplica posicao, vida, pocoes e inventario ao jogador da cena.
    // Chamada por F9, pelo EventManager ou por um botao de UI (On Click > CarregarJogo()).
    public void CarregarJogo()
    {
        if (!File.Exists(CaminhoDoArquivo))
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "nenhum save (JSON) encontrado");
            return;
        }

        var jogador = FindFirstObjectByType<Player>();

        if (jogador == null)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, "carregar (JSON) falhou: jogador nao encontrado");
            return;
        }

        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(CaminhoDoArquivo));

            jogador.transform.position = new Vector3(data.posX, data.posY, jogador.transform.position.z);

            // LoadHealth ja dispara o evento de mudanca de vida por dentro do modulo
            // Game.Health, entao o HUD se atualiza sozinho, sem precisar avisar ninguem aqui.
            jogador.LoadHealth(data.vidaAtual);

            var controles = jogador.GetComponent<ControlesDeVida>();
            if (controles != null) controles.DefinirPocoes(data.pocoes);

            var inventario = FindFirstObjectByType<Inventory>();
            if (inventario != null)
            {
                // Zera o inventario atual antes de recolocar o que veio do save,
                // senao os itens pegos depois do save ficariam somados aos salvos.
                inventario.slots.Clear();

                if (data.itens != null)
                {
                    foreach (ItemSalvo itemSalvo in data.itens)
                    {
                        ItemData item = inventario.itemDatabase != null
                            ? inventario.itemDatabase.GetItemByName(itemSalvo.nome)
                            : null;

                        if (item != null)
                        {
                            inventario.AddItem(item, itemSalvo.quantidade);
                        }
                    }
                }

                // Garante que a UI se atualize mesmo se nenhum item tiver sido
                // adicionado de volta (save antigo sem "itens", ou nomes que nao
                // batem mais com a ItemDatabase) - senao os icones antigos ficariam
                // na tela ate o proximo item ser coletado.
                ForcarAtualizacaoDaUI(inventario);
            }

            GameLog.Escrever(ActionLog.Tipo.Sistema, "jogo carregado via JSON");
        }
        catch (Exception e)
        {
            GameLog.Escrever(ActionLog.Tipo.Sistema, $"carregar (JSON) falhou: {e.Message}");
        }
    }

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