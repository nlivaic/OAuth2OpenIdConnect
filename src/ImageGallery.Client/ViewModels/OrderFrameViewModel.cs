namespace ImageGallery.Client
{
    public class OrderFrameViewModel
    {
        public string Address { get; private set; } = string.Empty;

        public OrderFrameViewModel(string address)
        {
            Address = address;
        }
    }
}