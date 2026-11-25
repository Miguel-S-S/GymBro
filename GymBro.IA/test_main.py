"""Pruebas del microservicio (python -m unittest discover -s GymBro.IA -p "test_*.py"). Ollama se reemplaza por un doble."""
import sys
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))
from fastapi.testclient import TestClient  # noqa: E402

import main  # noqa: E402


class GenerarEmbeddingTests(unittest.TestCase):
    def setUp(self):
        self.cliente = TestClient(main.app)

    @mock.patch("main.ollama.embeddings", return_value={"embedding": [0.1, 0.2, 0.3]})
    def test_texto_en_el_cuerpo(self, embeddings):
        r = self.cliente.post("/generar-embedding/", json={"texto": "sentadilla"})
        self.assertEqual(r.status_code, 200)
        self.assertEqual(r.json()["dimensiones"], 3)
        embeddings.assert_called_once_with(model=main.MODELO_EMBEDDINGS, prompt="sentadilla")

    @mock.patch("main.ollama.embeddings", return_value={"embedding": [0.5]})
    def test_texto_en_la_cadena_de_consulta_sigue_funcionando(self, embeddings):
        r = self.cliente.post("/generar-embedding/?texto=remo")
        self.assertEqual(r.status_code, 200)
        embeddings.assert_called_once_with(model=main.MODELO_EMBEDDINGS, prompt="remo")

    def test_sin_texto_responde_400(self):
        self.assertEqual(self.cliente.post("/generar-embedding/", json={"texto": "  "}).status_code, 400)
        self.assertEqual(self.cliente.post("/generar-embedding/").status_code, 400)

    @mock.patch("main.ollama.embeddings", side_effect=ConnectionError("apagado"))
    def test_ollama_caido_responde_503(self, _):
        r = self.cliente.post("/generar-embedding/", json={"texto": "sentadilla"})
        self.assertEqual(r.status_code, 503)


if __name__ == "__main__":
    unittest.main()
