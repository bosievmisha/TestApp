using System.Collections.Concurrent;
using System.Diagnostics;
using TestApp.Models;

namespace TestApp
{
    class Program
    {
        // Параметры
        static readonly int M = 100;
        static readonly int NumFactories = 3;
        static readonly int BaseProductionRate = 50;
        static readonly double WarehouseFullThreshold = 0.95;
        static readonly int SimulationTime = 100;

        // Общие ресурсы
        static BlockingCollection<(Factory Factory, Product Product, int Count)> Warehouse = new BlockingCollection<(Factory, Product, int)>(new ConcurrentQueue<(Factory, Product, int)>());
        static ConcurrentDictionary<string, int> ProductCounts = new ConcurrentDictionary<string, int>();
        static ConcurrentBag<Dictionary<string, int>> TruckLoads = new ConcurrentBag<Dictionary<string, int>>();
        static List<Truck> Trucks = new List<Truck>()
        {
            new Truck("Малый грузовик", 150),
            new Truck("Большой грузовик", 300)
        };

        static SemaphoreSlim WarehouseSemaphore = new SemaphoreSlim(1, 1);
        static CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        static void Main(string[] args)
        {
            var factories = InitializeFactories();
            var totalProductionRate = factories.Sum(f => f.ProductionRate);
            var warehouseCapacity = (int)(M * totalProductionRate);
            Console.WriteLine($"Вместимость склада: {warehouseCapacity} единиц продукции");

            var factoryTasks = factories.Select(f => Task.Run(() => Produce(f, CancellationTokenSource.Token))).ToArray();
            var warehouseTask = Task.Run(() => ManageWarehouse(warehouseCapacity, CancellationTokenSource.Token));

            Task.WaitAll(factoryTasks); // Ожидаем завершения только заводов
            
            Warehouse.CompleteAdding();
            CancellationTokenSource.Cancel();
            warehouseTask.Wait();

            PrintStatistics();
            Console.ReadKey();
        }

        static List<Factory> InitializeFactories()
        {
            var factories = new List<Factory>();
            for (int i = 0; i < NumFactories; i++)
            {
                char factoryName = (char)('A' + i);
                string productName = factoryName.ToString().ToLower();
                double productionRate = BaseProductionRate * (1 + i * 0.1);
                var product = new Product(productName, 1 + i * 0.2, $"Коробка {i + 1}");
                factories.Add(new Factory(factoryName.ToString(), product, productionRate));
            }
            return factories;
        }

        static void Produce(Factory factory, CancellationToken cancellationToken)
        {
            try
            {
                for (int hour = 0; hour < SimulationTime; hour++)
                {
                    int producedUnits = (int)factory.ProductionRate;

                    for (int i = 0; i < producedUnits; i++)
                    {
                        Warehouse.Add((factory, factory.Product, 1), cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    ProductCounts.AddOrUpdate(factory.Product.Name, producedUnits, (key, oldValue) => oldValue + producedUnits);

                    Console.WriteLine($"[Завод {factory.Name}] Поступило на склад за {hour + 1}-й час: {factory.Product} ({producedUnits} шт)");

                    Thread.Sleep(1000 / NumFactories);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Производство на заводе {factory.Name} отменено.");
            }
        }

        static void ManageWarehouse(int warehouseCapacity, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    WarehouseSemaphore.Wait(token);
                    try
                    {
                        if (Warehouse.Count >= warehouseCapacity * WarehouseFullThreshold)
                        {
                            Console.WriteLine($"\nСклад заполнен на {((double)Warehouse.Count / warehouseCapacity) * 100:F2}%. Начинается отгрузка.");
                            UnloadWarehouse(token);
                        }
                        
                    }
                    finally
                    {
                        WarehouseSemaphore.Release();
                    }

                    if (token.IsCancellationRequested)
                        token.ThrowIfCancellationRequested();

                    Thread.Sleep(1000);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Управление складом отменено.");
            }
            finally
            {
                WarehouseSemaphore.Dispose();
            }
        }

        static void UnloadWarehouse(CancellationToken token)
        {
            var itemsToUnload = new List<(Factory, Product, int)>();
            try
            {
                while (Warehouse.TryTake(out var item))
                {
                    if (token.IsCancellationRequested)
                    {
                        Warehouse.Add(item);
                        break;
                    }

                    itemsToUnload.Add(item);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!itemsToUnload.Any())
            {
                Console.WriteLine("На складе нет продукции для отгрузки.");
                return;
            }

            var random = new Random();
            var truck = Trucks[random.Next(Trucks.Count)];
            Console.WriteLine($"  Прибыл {truck.Name} (вместимость: {truck.Capacity} ед.)");

            var load = new Dictionary<string, int>();
            int remainingCapacity = truck.Capacity;
            var productsToReturn = new List<(Factory, Product, int)>();

            foreach (var productGroup in itemsToUnload.GroupBy(x => x.Item2.Name))
            {
                int totalProductCount = productGroup.Sum(x => x.Item3);
                int loadCount = Math.Min(remainingCapacity, totalProductCount);

                load[productGroup.Key] = loadCount;
                remainingCapacity -= loadCount;

                int returnedCount = totalProductCount - loadCount;
                if (returnedCount > 0)
                {
                    for (int i = 0; i < returnedCount; i++)
                    {
                        productsToReturn.Add((productGroup.First().Item1, productGroup.First().Item2, 1));
                    }
                }
            }

            foreach (var productToReturn in productsToReturn)
            {
                Warehouse.Add(productToReturn, token);
            }

            TruckLoads.Add(load);

            Console.WriteLine($"    {truck.Name} загружен:");
            foreach (var item in load)
            {
                Console.WriteLine($"      {item.Key}: {item.Value} шт.");
            }
        }

        static void PrintStatistics()
        {
            Console.WriteLine("\n----- Статистика перевозок -----");

            if (TruckLoads.Any())
            {
                var averageLoads = new Dictionary<string, double>();
                foreach (var load in TruckLoads)
                {
                    foreach (var product in load)
                    {
                        if (averageLoads.ContainsKey(product.Key))
                            averageLoads[product.Key] += product.Value;
                        else
                            averageLoads[product.Key] = product.Value;
                    }
                }

                // !!! Сортировка по имени продукта
                foreach (var product in averageLoads.OrderBy(p => p.Key))
                {
                    averageLoads[product.Key] /= TruckLoads.Count;
                    Console.WriteLine($"- В среднем грузовики перевозят: {product.Key} - {averageLoads[product.Key]:F2} шт.");
                }
            }
            else
            {
                Console.WriteLine("Перевозок не было.");
            }

            Console.WriteLine("\n----- Итоговое количество произведенной продукции -----");

            // !!! Сортировка по имени продукта
            foreach (var productCount in ProductCounts.OrderBy(p => p.Key))
            {
                Console.WriteLine($"- {productCount.Key}: {productCount.Value} шт.");
            }
        }

    }
}