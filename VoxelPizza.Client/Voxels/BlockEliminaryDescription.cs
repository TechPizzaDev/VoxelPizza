namespace VoxelPizza.Client
{
    public readonly struct BlockEliminaryDescription
    {
        public CubeFaces BlockingFaces { get; }
        public CubeFaces OppositeBlockingFaces { get; }
        public BlockVisualFeatures Features { get; }

        public BlockEliminaryDescription(CubeFaces blockingFaces, BlockVisualFeatures features)
        {
            BlockingFaces = blockingFaces;
            OppositeBlockingFaces = blockingFaces.Opposite();
            Features = features;
        }
    }
}