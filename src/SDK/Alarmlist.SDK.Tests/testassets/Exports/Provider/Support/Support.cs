namespace Fixture
{
    public static class Support
    {
        public static byte[] Payload() => new byte[] { 0, 255, 128 };
        public static string Runtime =>
#if NET8_0_OR_GREATER
            "net8.0";
#else
            "net472";
#endif
    }
}
