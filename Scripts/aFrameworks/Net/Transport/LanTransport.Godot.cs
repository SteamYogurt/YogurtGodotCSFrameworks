public partial class LanTransport
{
    public LanTransport() : this(data => NetManager.Instance?.ProcessIncoming(data)) { }
}
