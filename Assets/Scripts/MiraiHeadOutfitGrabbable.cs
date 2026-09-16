using System.Collections.Generic;
using UnityEngine;

public class MiraiHeadOutfitGrabbable : MiraiHeadOutfit
{
    public Transform bone;
    public List<Collider> triggers = new List<Collider>();
}