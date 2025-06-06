// PlayerProgressSO.cs
using UnityEngine;

public class PlayerProgressSO : MonoBehaviour
{
    public static PlayerProgressSO Instance { get; private set; }
    public PlayerProgress data;               // อ้างถึง asset

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (data == null)
            data = Resources.Load<PlayerProgress>("PlayerProgress");
    }
    public bool HasCard(string id) => data.ownedCardIds.Contains(id);
    public void AddCard(string id)
    {
        if (!data.ownedCardIds.Contains(id))
            data.ownedCardIds.Add(id);
    }
}
