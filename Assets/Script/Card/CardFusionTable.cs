using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "CrossClash/Card Fusion Table")]
public class CardFusionTable : ScriptableObject
{
    [System.Serializable]
    public struct Recipe
    {
        public CardData a;      // วัตถุดิบ A
        public CardData b;      // วัตถุดิบ B
        public CardData result; // ผลลัพธ์ (ควรกำหนด category = FusionCard)
    }

    public List<Recipe> recipes = new();

    private Dictionary<(string, string), CardData> map;

    void OnEnable() => BuildMap();

    public void BuildMap()
    {
        map = new();
        foreach (var r in recipes)
        {
            if (!r.a || !r.b || !r.result) continue;
            map[(r.a.id, r.b.id)] = r.result;
            map[(r.b.id, r.a.id)] = r.result; // สลับข้างได้
        }
    }

    public CardData TryFuse(CardData x, CardData y)
    {
        if (!x || !y) return null;
        if (map == null || map.Count == 0) BuildMap();
        map.TryGetValue((x.id, y.id), out var res);
        return res;
    }
}
