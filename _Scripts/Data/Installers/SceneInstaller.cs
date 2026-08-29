using UnityEngine;
using Zenject;

public class SceneInstaller : MonoInstaller
{
    [SerializeField] private bool _isOnSeat;

    [SerializeField] private Animator _playerAnimator;

    [Header("Configs")]
    [SerializeField] private GameSettingsSO _gameSettings;

    [Header("Scene References")]
    [SerializeField] private CameraController _cameraController;

    [Header("Prefabs")]
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private GameObject _chickenPrefab; // Префаб курицы со скриптом Chicken

    public override void InstallBindings()
    {
        BindSettings();
        BindInfrastructure();
        BindEntities();
        BindControllers();
        BindSpawners();
    }

    private void BindSettings()
    {
        Container.BindInstance(_gameSettings).AsSingle();
        Container.BindInstance(_gameSettings.Camera).AsSingle();
        Container.BindInstance(_gameSettings.Player).AsSingle();
    }

    private void BindInfrastructure()
    {
        Container.Bind(typeof(IEntityRegistry<IEntity>)).To(typeof(EntityRegistry<IEntity>)).AsSingle();
    }
    private void BindEntities()
    {
        Container.Bind<Animator>().FromInstance(_playerAnimator).AsSingle();

        Container.Bind<Player>().FromComponentInNewPrefab(_playerPrefab).AsSingle();
        Container.Bind<IEntity>().To<Player>().FromResolve();

        // Добавляем привязку для машины на сцене:
        Container.Bind<Vechicle>().FromComponentInHierarchy().AsSingle();
        Container.Bind<IEntity>().To<Vechicle>().FromResolve();

        Container.Bind<CameraController>().FromInstance(_cameraController).AsSingle();

        if(_chickenPrefab != null) 
        Container.BindFactory<Chicken, Chicken.Factory>()
            .FromComponentInNewPrefab(_chickenPrefab)
            .AsTransient();
    }
    private void BindControllers()
    {
        if (!_isOnSeat)
            Container.BindInterfacesAndSelfTo<PlayerInputController>().AsSingle();
    }

    private void BindSpawners()
    {
        if (_chickenPrefab != null)
            Container.BindInterfacesAndSelfTo<ChickenSpawner>().AsSingle();
    }
}