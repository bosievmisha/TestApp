using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.Models
{
    public class Product
    {
        public string Name { get; set; }
        public double Weight { get; set; }
        public string PackageType { get; set; }

        public Product(string name, double weight, string packageType)
        {
            Name = name;
            Weight = weight;
            PackageType = packageType;
        }

        public override string ToString()
        {
            return $"{Name} ({Weight} кг, {PackageType})";
        }
    }
}
