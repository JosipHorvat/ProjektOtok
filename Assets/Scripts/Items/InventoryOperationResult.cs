/// <summary>Describes the outcome of a carry-inventory operation.</summary>
public enum InventoryOperationFailure
{
    None = 0,
    InvalidDefinition = 1,
    InvalidQuantity = 2,
    KeyItemsUseSeparateCollection = 3,
    DuplicateInstance = 4,
    ItemNotFound = 5,
    OutOfBounds = 6,
    Overlap = 7,
    RotationNotAllowed = 8,
    NoSpace = 9,
    NotStackable = 10,
    IncompatibleStack = 11,
    SameInstance = 12,
    StackLimitReached = 13,
    ProtectedItem = 14,
    InvalidDimensions = 15,
    ResizeWouldOrphanItems = 16,
    InvalidUpgrade = 17,
    NotStorable = 18,
    ItemEquipped = 19,
    MaximumCapacity = 20,
}

/// <summary>Immutable result returned by carry-inventory operations, including partial pickup acceptance.</summary>
public readonly struct InventoryOperationResult
{
    private InventoryOperationResult(
        bool succeeded,
        InventoryOperationFailure failure,
        int requestedQuantity,
        int acceptedQuantity)
    {
        Succeeded = succeeded;
        Failure = failure;
        RequestedQuantity = requestedQuantity;
        AcceptedQuantity = acceptedQuantity;
    }

    /// <summary>Gets whether the operation accepted or changed any valid inventory state.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets whether the complete requested quantity was accepted.</summary>
    public bool Completed => Succeeded && RemainingQuantity == 0;

    /// <summary>Gets the failure reason, or None when the operation accepted state.</summary>
    public InventoryOperationFailure Failure { get; }

    /// <summary>Gets the requested quantity for stack operations.</summary>
    public int RequestedQuantity { get; }

    /// <summary>Gets the quantity accepted by the inventory.</summary>
    public int AcceptedQuantity { get; }

    /// <summary>Gets the quantity that remains with the source after the operation.</summary>
    public int RemainingQuantity => RequestedQuantity - AcceptedQuantity;

    /// <summary>Creates a result for a fully successful non-stack operation.</summary>
    public static InventoryOperationResult Success()
    {
        return new InventoryOperationResult(true, InventoryOperationFailure.None, 0, 0);
    }

    /// <summary>Creates a result for an accepted stack quantity, including partial acceptance.</summary>
    public static InventoryOperationResult Accepted(int requestedQuantity, int acceptedQuantity)
    {
        return new InventoryOperationResult(
            acceptedQuantity > 0,
            acceptedQuantity > 0 ? InventoryOperationFailure.None : InventoryOperationFailure.NoSpace,
            requestedQuantity,
            acceptedQuantity);
    }

    /// <summary>Creates a failed result without mutating inventory state.</summary>
    public static InventoryOperationResult Rejected(InventoryOperationFailure failure, int requestedQuantity = 0)
    {
        return new InventoryOperationResult(false, failure, requestedQuantity, 0);
    }
}
