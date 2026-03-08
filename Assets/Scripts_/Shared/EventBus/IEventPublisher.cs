namespace Shared.EventBus
{
    public interface IEventPublisher
    {
        void Publish<T>(T e);
    }
}
