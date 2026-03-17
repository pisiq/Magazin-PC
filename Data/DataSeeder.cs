using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Models;

namespace Recomandare_PC.Data;

public static class DataSeeder
{
    public static async Task SeedProductsAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync()) return; // already seeded

        var products = new List<Product>
        {
            // ── CPU (CategoryId = 1) ──────────────────────────────────────────
            new() { CategoryId = 1, Name = "AMD Ryzen 5 7600X",       Price = 899m,  StockQuantity = 10,
                Specifications = """{"Socket":"AM5","Cores":"6","Threads":"12","Base Clock":"4.7 GHz","Boost Clock":"5.3 GHz","TDP":"105W","Cache L3":"32 MB"}""" },
            new() { CategoryId = 1, Name = "AMD Ryzen 7 7700X",       Price = 1299m, StockQuantity = 7,
                Specifications = """{"Socket":"AM5","Cores":"8","Threads":"16","Base Clock":"4.5 GHz","Boost Clock":"5.4 GHz","TDP":"105W","Cache L3":"32 MB"}""" },
            new() { CategoryId = 1, Name = "Intel Core i5-13600K",    Price = 1149m, StockQuantity = 8,
                Specifications = """{"Socket":"LGA1700","Cores":"14 (6P+8E)","Threads":"20","Base Clock":"3.5 GHz","Boost Clock":"5.1 GHz","TDP":"125W","Cache L3":"24 MB"}""" },
            new() { CategoryId = 1, Name = "Intel Core i7-13700K",    Price = 1799m, StockQuantity = 5,
                Specifications = """{"Socket":"LGA1700","Cores":"16 (8P+8E)","Threads":"24","Base Clock":"3.4 GHz","Boost Clock":"5.4 GHz","TDP":"125W","Cache L3":"30 MB"}""" },
            new() { CategoryId = 1, Name = "AMD Ryzen 5 5600X",       Price = 599m,  StockQuantity = 12,
                Specifications = """{"Socket":"AM4","Cores":"6","Threads":"12","Base Clock":"3.7 GHz","Boost Clock":"4.6 GHz","TDP":"65W","Cache L3":"32 MB"}""" },

            // ── GPU (CategoryId = 2) ──────────────────────────────────────────
            new() { CategoryId = 2, Name = "NVIDIA GeForce RTX 4060", Price = 1799m, StockQuantity = 12,
                Specifications = """{"VRAM":"8 GB GDDR6","CUDA Cores":"3072","Base Clock":"1830 MHz","Boost Clock":"2460 MHz","TDP":"115W","Interface":"PCIe 4.0 x8"}""" },
            new() { CategoryId = 2, Name = "NVIDIA GeForce RTX 4070", Price = 2799m, StockQuantity = 6,
                Specifications = """{"VRAM":"12 GB GDDR6X","CUDA Cores":"5888","Base Clock":"1920 MHz","Boost Clock":"2475 MHz","TDP":"200W","Interface":"PCIe 4.0 x16"}""" },
            new() { CategoryId = 2, Name = "AMD Radeon RX 7600",      Price = 1499m, StockQuantity = 9,
                Specifications = """{"VRAM":"8 GB GDDR6","Stream Processors":"2048","Base Clock":"1720 MHz","Boost Clock":"2655 MHz","TDP":"165W","Interface":"PCIe 4.0 x8"}""" },
            new() { CategoryId = 2, Name = "AMD Radeon RX 7700 XT",   Price = 2099m, StockQuantity = 7,
                Specifications = """{"VRAM":"12 GB GDDR6","Stream Processors":"3456","Base Clock":"1700 MHz","Boost Clock":"2544 MHz","TDP":"245W","Interface":"PCIe 4.0 x16"}""" },
            new() { CategoryId = 2, Name = "NVIDIA GeForce RTX 4060 Ti", Price = 2299m, StockQuantity = 5,
                Specifications = """{"VRAM":"8 GB GDDR6","CUDA Cores":"4352","Base Clock":"2310 MHz","Boost Clock":"2535 MHz","TDP":"160W","Interface":"PCIe 4.0 x16"}""" },

            // ── RAM DDR4 (CategoryId = 3, SubcategoryId = 6) ─────────────────
            new() { CategoryId = 3, SubcategoryId = 6, Name = "Kingston Fury Beast DDR4 16GB 3200MHz", Price = 299m, StockQuantity = 20,
                Specifications = """{"Type":"DDR4","Capacity":"16 GB (2x8GB)","Speed":"3200 MHz","CAS Latency":"CL16","Voltage":"1.35V","Profile":"XMP 2.0"}""" },
            new() { CategoryId = 3, SubcategoryId = 6, Name = "G.Skill Trident Z DDR4 32GB 3600MHz",  Price = 499m, StockQuantity = 10,
                Specifications = """{"Type":"DDR4","Capacity":"32 GB (2x16GB)","Speed":"3600 MHz","CAS Latency":"CL16","Voltage":"1.35V","Profile":"XMP 2.0"}""" },
            new() { CategoryId = 3, SubcategoryId = 6, Name = "Corsair Vengeance LPX DDR4 16GB 2666MHz", Price = 229m, StockQuantity = 18,
                Specifications = """{"Type":"DDR4","Capacity":"16 GB (2x8GB)","Speed":"2666 MHz","CAS Latency":"CL16","Voltage":"1.2V"}""" },

            // ── RAM DDR5 (CategoryId = 3, SubcategoryId = 7) ─────────────────
            new() { CategoryId = 3, SubcategoryId = 7, Name = "Corsair Vengeance DDR5 32GB 5600MHz",   Price = 649m, StockQuantity = 15,
                Specifications = """{"Type":"DDR5","Capacity":"32 GB (2x16GB)","Speed":"5600 MHz","CAS Latency":"CL36","Voltage":"1.25V","Profile":"XMP 3.0"}""" },
            new() { CategoryId = 3, SubcategoryId = 7, Name = "Kingston Fury Beast DDR5 32GB 6000MHz", Price = 749m, StockQuantity = 8,
                Specifications = """{"Type":"DDR5","Capacity":"32 GB (2x16GB)","Speed":"6000 MHz","CAS Latency":"CL36","Voltage":"1.35V","Profile":"XMP 3.0"}""" },

            // ── Motherboard (CategoryId = 4) ──────────────────────────────────
            new() { CategoryId = 4, Name = "ASUS PRIME B650-PLUS",         Price = 799m,  StockQuantity = 8,
                Specifications = """{"Socket":"AM5","Chipset":"B650","Form Factor":"ATX","Memory Slots":"4x DDR5","Max RAM":"128 GB","PCIe":"1x PCIe 4.0 x16","M.2 Slots":"2"}""" },
            new() { CategoryId = 4, Name = "MSI MAG B760 TOMAHAWK WiFi",   Price = 849m,  StockQuantity = 7,
                Specifications = """{"Socket":"LGA1700","Chipset":"B760","Form Factor":"ATX","Memory Slots":"4x DDR5","Max RAM":"192 GB","PCIe":"1x PCIe 5.0 x16","M.2 Slots":"3"}""" },
            new() { CategoryId = 4, Name = "Gigabyte B550 AORUS ELITE V2", Price = 699m,  StockQuantity = 6,
                Specifications = """{"Socket":"AM4","Chipset":"B550","Form Factor":"ATX","Memory Slots":"4x DDR4","Max RAM":"128 GB","PCIe":"1x PCIe 4.0 x16","M.2 Slots":"2"}""" },
            new() { CategoryId = 4, Name = "ASUS ROG STRIX Z790-F",        Price = 1799m, StockQuantity = 4,
                Specifications = """{"Socket":"LGA1700","Chipset":"Z790","Form Factor":"ATX","Memory Slots":"4x DDR5","Max RAM":"192 GB","PCIe":"1x PCIe 5.0 x16","M.2 Slots":"5"}""" },

            // ── PSU (CategoryId = 5) ──────────────────────────────────────────
            new() { CategoryId = 5, Name = "Seasonic Focus GX-650W",              Price = 499m, StockQuantity = 12,
                Specifications = """{"Wattage":"650W","Efficiency":"80+ Gold","Modular":"Full Modular","Fan":"120mm","ATX12V":"2.4","MTBF":"100000h"}""" },
            new() { CategoryId = 5, Name = "Corsair RM750e 80+ Gold",             Price = 599m, StockQuantity = 10,
                Specifications = """{"Wattage":"750W","Efficiency":"80+ Gold","Modular":"Full Modular","Fan":"120mm","ATX12V":"2.52","MTBF":"100000h"}""" },
            new() { CategoryId = 5, Name = "be quiet! Straight Power 11 850W",    Price = 749m, StockQuantity = 6,
                Specifications = """{"Wattage":"850W","Efficiency":"80+ Gold","Modular":"Full Modular","Fan":"135mm","ATX12V":"2.4","MTBF":"100000h"}""" },
            new() { CategoryId = 5, Name = "Thermaltake Toughpower GF1 650W",     Price = 399m, StockQuantity = 9,
                Specifications = """{"Wattage":"650W","Efficiency":"80+ Gold","Modular":"Full Modular","Fan":"140mm","ATX12V":"2.4"}""" },

            // ── Storage NVMe (CategoryId = 6, SubcategoryId = 3) ─────────────
            new() { CategoryId = 6, SubcategoryId = 3, Name = "Samsung 980 Pro 1TB NVMe",   Price = 449m, StockQuantity = 18,
                Specifications = """{"Interface":"PCIe 4.0 NVMe","Capacity":"1 TB","Sequential Read":"7000 MB/s","Sequential Write":"5000 MB/s","NAND":"V-NAND MLC","Form Factor":"M.2 2280","TBW":"600 TB"}""" },
            new() { CategoryId = 6, SubcategoryId = 3, Name = "WD Blue SN580 1TB NVMe",     Price = 329m, StockQuantity = 15,
                Specifications = """{"Interface":"PCIe 4.0 NVMe","Capacity":"1 TB","Sequential Read":"4150 MB/s","Sequential Write":"4150 MB/s","NAND":"TLC","Form Factor":"M.2 2280","TBW":"600 TB"}""" },
            new() { CategoryId = 6, SubcategoryId = 3, Name = "Kingston NV3 2TB NVMe",      Price = 499m, StockQuantity = 10,
                Specifications = """{"Interface":"PCIe 4.0 NVMe","Capacity":"2 TB","Sequential Read":"6000 MB/s","Sequential Write":"5000 MB/s","NAND":"TLC","Form Factor":"M.2 2280","TBW":"1200 TB"}""" },

            // ── Storage SATA SSD (CategoryId = 6, SubcategoryId = 4) ─────────
            new() { CategoryId = 6, SubcategoryId = 4, Name = "Crucial MX500 500GB SATA SSD", Price = 229m, StockQuantity = 20,
                Specifications = """{"Interface":"SATA III","Capacity":"500 GB","Sequential Read":"560 MB/s","Sequential Write":"510 MB/s","NAND":"3D TLC","Form Factor":"2.5\"","TBW":"180 TB"}""" },
            new() { CategoryId = 6, SubcategoryId = 4, Name = "Samsung 870 EVO 1TB SATA",    Price = 399m, StockQuantity = 12,
                Specifications = """{"Interface":"SATA III","Capacity":"1 TB","Sequential Read":"560 MB/s","Sequential Write":"530 MB/s","NAND":"V-NAND MLC","Form Factor":"2.5\"","TBW":"600 TB"}""" },

            // ── Storage HDD (CategoryId = 6, SubcategoryId = 5) ──────────────
            new() { CategoryId = 6, SubcategoryId = 5, Name = "Seagate Barracuda 2TB HDD",   Price = 199m, StockQuantity = 14,
                Specifications = """{"Interface":"SATA III","Capacity":"2 TB","RPM":"7200 RPM","Cache":"256 MB","Form Factor":"3.5\""}""" },
            new() { CategoryId = 6, SubcategoryId = 5, Name = "WD Blue 4TB HDD",             Price = 329m, StockQuantity = 8,
                Specifications = """{"Interface":"SATA III","Capacity":"4 TB","RPM":"5400 RPM","Cache":"256 MB","Form Factor":"3.5\""}""" },

            // ── Cooler Aer (CategoryId = 7, SubcategoryId = 1) ───────────────
            new() { CategoryId = 7, SubcategoryId = 1, Name = "be quiet! Pure Rock 2",    Price = 149m, StockQuantity = 15,
                Specifications = """{"Type":"Air Cooler","Fan Size":"120mm","TDP Support":"150W","Height":"155mm","Socket":"AM4,AM5,LGA1700"}""" },
            new() { CategoryId = 7, SubcategoryId = 1, Name = "Noctua NH-D15",            Price = 399m, StockQuantity = 8,
                Specifications = """{"Type":"Air Cooler","Fan Size":"2x 140mm","TDP Support":"250W","Height":"165mm","Socket":"AM4,AM5,LGA1700","Heat Pipes":"6"}""" },
            new() { CategoryId = 7, SubcategoryId = 1, Name = "Deepcool AK620",           Price = 249m, StockQuantity = 11,
                Specifications = """{"Type":"Air Cooler","Fan Size":"2x 120mm","TDP Support":"260W","Height":"160mm","Socket":"AM4,AM5,LGA1700","Heat Pipes":"6"}""" },

            // ── Cooler Lichid (CategoryId = 7, SubcategoryId = 2) ────────────
            new() { CategoryId = 7, SubcategoryId = 2, Name = "Corsair H100i RGB Elite 240mm",  Price = 499m, StockQuantity = 7,
                Specifications = """{"Type":"Liquid Cooling AIO","Radiator":"240mm","Fan Size":"2x 120mm","Pump Speed":"2400 RPM","Socket":"AM4,AM5,LGA1700"}""" },
            new() { CategoryId = 7, SubcategoryId = 2, Name = "NZXT Kraken 360 RGB",           Price = 699m, StockQuantity = 5,
                Specifications = """{"Type":"Liquid Cooling AIO","Radiator":"360mm","Fan Size":"3x 120mm","Pump Speed":"2800 RPM","Socket":"AM4,AM5,LGA1700"}""" },

            // ── Case (CategoryId = 8) ─────────────────────────────────────────
            new() { CategoryId = 8, Name = "Fractal Design Define 7",  Price = 549m, StockQuantity = 6,
                Specifications = """{"Form Factor":"Mid Tower","Motherboard":"ATX,mATX,mITX","Tempered Glass":"Yes","Drive Bays":"2x 3.5\",2x 2.5\"","Front Fans":"2x 140mm","Dimensions":"543x233x466 mm"}""" },
            new() { CategoryId = 8, Name = "NZXT H510 Flow",           Price = 399m, StockQuantity = 9,
                Specifications = """{"Form Factor":"Mid Tower","Motherboard":"ATX,mATX,mITX","Tempered Glass":"Yes","Drive Bays":"2x 3.5\",2x 2.5\"","Front Fans":"2x 120mm","Dimensions":"428x210x460 mm"}""" },
            new() { CategoryId = 8, Name = "Lian Li Lancool III",      Price = 499m, StockQuantity = 5,
                Specifications = """{"Form Factor":"Mid Tower","Motherboard":"ATX,mATX,mITX,E-ATX","Tempered Glass":"Yes","Drive Bays":"4x 2.5\"","Front Fans":"3x 140mm","Dimensions":"494x234x511 mm"}""" },
            new() { CategoryId = 8, Name = "Corsair 4000D Airflow",    Price = 449m, StockQuantity = 7,
                Specifications = """{"Form Factor":"Mid Tower","Motherboard":"ATX,mATX,mITX","Tempered Glass":"Yes","Drive Bays":"2x 3.5\",2x 2.5\"","Front Fans":"2x 120mm","Dimensions":"466x230x453 mm"}""" },
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }
}
