namespace VoxelPizza.Collections.Bits;

public interface IBitPartTracker<P>
{
    static abstract bool ReportChanges { get; }
    
    void Setup(int bitsPerElement, int elementsPerPart);

    void PartChanged(P prev, P next, int bitsPerElement);
}
