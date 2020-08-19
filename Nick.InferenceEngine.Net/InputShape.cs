namespace Nick.InferenceEngine.Net
{
    public class InputShape
    {
        public string Name { get; }
        public dimensions_t Dimensions;

        public InputShape(string name, dimensions_t dimensions)
        {
            Name = name;
            Dimensions = dimensions;
        }
    }
}
