import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from convert import jsonable


class FontDataConversion(unittest.TestCase):
    def test_text_layout_dictionary_survives(self):
        original = {"m_FontData": {"m_FontSize": 8, "m_Alignment": 4,
                                    "m_Font": {"m_FileID": 2, "m_PathID": 528},
                                    "m_LineSpacing": 0.75}}
        self.assertEqual(jsonable(original), original)

    def test_binary_font_payload_stays_stripped(self):
        self.assertEqual(jsonable({"m_FontData": b"font bytes"}),
                         {"m_FontData": {"$stripped": 10}})

    def test_legacy_numeric_font_payload_stays_stripped(self):
        self.assertEqual(jsonable({"m_FontData": [1, 2, 3]}),
                         {"m_FontData": {"$stripped": 3}})


if __name__ == "__main__":
    unittest.main()
