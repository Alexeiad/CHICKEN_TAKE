using Zenject;

public class ProjectAudioInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<AudioSettings>()
            .AsSingle()
            .NonLazy();
    }
}