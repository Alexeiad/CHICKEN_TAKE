using UnityEngine;
using UnityEngine.Splines;
using Zenject;

public class ChickenSpawner : IInitializable
{
    private readonly Chicken.Factory _chickenFactory;

    public ChickenSpawner(Chicken.Factory chickenFactory)
    {
        _chickenFactory = chickenFactory;
    }

    public void Initialize()
    {
        // Находим все контейнеры сплайнов на сцене
        SplineContainer[] splineContainers = Object.FindObjectsByType<SplineContainer>(FindObjectsSortMode.None);

        foreach (var container in splineContainers)
        {
            foreach (var spline in container.Splines)
            {
                // Проверяем, является ли сплайн замкнутым
                if (spline.Closed)
                {
                    // Рандомное количество кур от 30 до 50 (включительно)
                    int chickenCount = Random.Range(40, 50);

                    for (int i = 0; i < chickenCount; i++)
                    {
                        // Вычисляем нормализованную позицию t от 0 до 1 вдоль сплайна
                        float t = (float)i / chickenCount;

                        // Получаем локальную позицию на сплайне и переводим в мировые координаты
                        Vector3 localPos = spline.EvaluatePosition(t);
                        Vector3 worldPos = container.transform.TransformPoint(localPos);

                        // Создаем курицу через фабрику Zenject
                        Chicken chicken = _chickenFactory.Create();
                        chicken.transform.position = worldPos;
                        chicken.transform.rotation = Quaternion.identity;

                        // Опционально: делаем дочерним для удобства в иерархии
                        chicken.transform.SetParent(container.transform);
                    }
                }
            }
        }
    }
}