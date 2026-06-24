import importlib.util
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "物价补丁" / "tools"


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


class PriceMarkerCleanupTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.name_patch = load_module(
            "name_patch_cleanup", TOOLS / "poe2_name_price_patch.py"
        )
        cls.price_patch = load_module(
            "price_patch_cleanup", TOOLS / "build_poe2scout_price_patch.py"
        )

    def test_base_item_cleanup_only_removes_price_suffix(self):
        strip = self.name_patch.strip_existing_price_suffix
        self.assertEqual(strip("卡兰德的魔镜=12D", "="), "卡兰德的魔镜")
        self.assertEqual(strip("低价物品=<1E", "="), "低价物品")
        self.assertEqual(strip("A=大功能名", "="), "A=大功能名")

    def test_build_replacements_refreshes_and_cleans_stale_prices(self):
        BaseItemName = self.name_patch.BaseItemName
        entries = [
            BaseItemName(0, "Metadata/Items/A", "卡兰德的魔镜=12D", 0, 100, 0, 20),
            BaseItemName(1, "Metadata/Items/B", "过期物品=3E", 20, 104, 20, 40),
            BaseItemName(2, "Metadata/Items/C", "A=大功能名", 40, 108, 40, 60),
        ]
        rows = [{"metadata_path": "Metadata/Items/A", "price": "15D"}]

        replacements, warnings = self.name_patch.build_replacements(
            entries=entries,
            price_rows=rows,
            separator="=",
            keep_existing_price=True,
            mode="append",
            patch_same_name_duplicates=True,
        )

        by_row = {item.row_index: item.fitted_name for item in replacements}
        self.assertEqual(warnings, [])
        self.assertEqual(by_row[0], "卡兰德的魔镜=15D")
        self.assertEqual(by_row[1], "过期物品")
        self.assertNotIn(2, by_row)

    def test_unique_price_cleanup_accepts_all_generated_forms(self):
        strip = self.price_patch.strip_existing_price
        self.assertEqual(strip("[12D|卡兰德的魔镜]"), "卡兰德的魔镜")
        self.assertEqual(strip("[<1E|低价传奇]"), "低价传奇")
        self.assertEqual(strip("传奇名\n[12.5D]"), "传奇名")
        self.assertEqual(strip("传奇名<<[<1D]>>"), "传奇名")
        self.assertEqual(strip("普通名=不是价格"), "普通名=不是价格")


if __name__ == "__main__":
    unittest.main()
