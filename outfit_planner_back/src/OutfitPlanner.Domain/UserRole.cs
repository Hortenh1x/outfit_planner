namespace OutfitPlanner.Domain;

// Account access tier (paywall foundation). Free must stay first so accounts persisted
// before roles existed (e.g. local snapshots without the field) deserialize as Free.
public enum UserRole
{
    Free = 0,
    Premium,
    Admin
}
