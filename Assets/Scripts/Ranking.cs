using System;
using System.Collections.Generic;
using UnityEngine;

// Ranking local: os melhores resultados deste aparelho, salvos em PlayerPrefs (no navegador, no IndexedDB do site).
public static class Ranking
{
    public const int Size = 10;
    const string Key = "ranking";

    [Serializable]
    public class Entry
    {
        public int score;
        public long ticks; // data e hora (DateTime.Ticks)
        public DateTime Date => new DateTime(ticks);
    }

    [Serializable]
    class Data { public List<Entry> entries = new List<Entry>(); }

    static Data data;

    public static IReadOnlyList<Entry> Entries
    {
        get { Load(); return data.entries; }
    }

    static void Load()
    {
        if (data != null) return;
        try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key, "")); }
        catch (ArgumentException) { data = null; }
        if (data == null) data = new Data();

        // Quem já jogava antes do ranking existir: o recorde antigo entra como primeira linha
        int oldBest = PlayerPrefs.GetInt("best", 0);
        if (data.entries.Count == 0 && oldBest > 0)
        {
            data.entries.Add(new Entry { score = oldBest, ticks = DateTime.Now.Ticks });
            Save();
        }
    }

    static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // Registra uma partida. Devolve a posição (1 = primeiro) ou 0 se não entrou no ranking.
    public static int Submit(int score, out Entry added)
    {
        Load();
        added = null;
        if (score <= 0) return 0;

        // Empate: o resultado mais antigo fica na frente
        int pos = data.entries.FindIndex(e => score > e.score);
        if (pos < 0) pos = data.entries.Count;
        if (pos >= Size) return 0;

        added = new Entry { score = score, ticks = DateTime.Now.Ticks };
        data.entries.Insert(pos, added);
        if (data.entries.Count > Size) data.entries.RemoveRange(Size, data.entries.Count - Size);
        Save();
        return pos + 1;
    }
}
