using UnityEngine;

public enum TutorialEvent
{
    None = 0,
    GameStart,
    FirstWordConfirmed,
    DictionaryOpened,
    FirstTilePlaced,
    BagLow,
    ManaEmpty,
    FirstSpecialUsed,

        // ==== เพิ่มสำหรับหน้า Shop ====
    ShopOpened,
    ShopReroll,
    ShopBuy           // << ต้องการอันนี้
}
