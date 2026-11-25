"""Etapa 9b-2: pruebas del endpoint de sustitutos con Ollama reemplazado por un doble (python -m unittest discover -s GymBro.IA -p "test_*.py")."""
import json
import os
import sys
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))
from fastapi.testclient import TestClient  # noqa: E402

import main  # noqa: E402
from sustitutos import armar_mensajes, PeticionSustitutos, respeta_conjunto  # noqa: E402

PEDIDO = {
    "ejercicio": {"id": "a", "nombre": "Sentadilla trasera", "zona": "Legs"},
    "motivo": "molestia lumbar",
    "candidatos": [{"id": "b", "nombre": "Prensa"}, {"id": "c", "nombre": "Extension de rodillas"}],
    "maximo": 2,
}


class SustitutosTests(unittest.TestCase):
    def setUp(self):
        self.cliente = TestClient(main.app)

    @mock.patch.dict(os.environ, {"OLLAMA_MODELO_GENERACION": "modelo-prueba"})
    @mock.patch("sustitutos.ollama.chat", return_value={"message": {"content": "{\"elegidos\": [{\"id\": \"b\", \"justificacion\": \"x\"}]}"}})
    def test_devuelve_el_texto_del_modelo_y_pide_json_con_temperatura_0(self, chat):
        r = self.cliente.post("/sugerir-sustitutos/", json=PEDIDO)
        self.assertEqual(r.status_code, 200)
        self.assertEqual(r.json()["modelo"], "modelo-prueba")
        self.assertIn("elegidos", json.loads(r.json()["respuesta"]))
        args = chat.call_args.kwargs
        self.assertEqual(args["format"], "json")
        self.assertEqual(args["options"]["temperature"], 0)
        # El pedido lista solo los candidatos recibidos, por id.
        self.assertIn("id: b", args["messages"][1]["content"])
        self.assertIn("id: c", args["messages"][1]["content"])

    @mock.patch.dict(os.environ, {"OLLAMA_MODELO_GENERACION": ""})
    def test_sin_modelo_configurado_responde_503(self):
        self.assertEqual(self.cliente.post("/sugerir-sustitutos/", json=PEDIDO).status_code, 503)

    @mock.patch.dict(os.environ, {"OLLAMA_MODELO_GENERACION": "modelo-prueba"})
    @mock.patch("sustitutos.ollama.chat", side_effect=ConnectionError("apagado"))
    def test_ollama_caido_responde_503(self, _):
        self.assertEqual(self.cliente.post("/sugerir-sustitutos/", json=PEDIDO).status_code, 503)

    def test_sin_candidatos_o_motivo_responde_422(self):
        self.assertEqual(self.cliente.post("/sugerir-sustitutos/", json={**PEDIDO, "candidatos": []}).status_code, 422)
        self.assertEqual(self.cliente.post("/sugerir-sustitutos/", json={**PEDIDO, "motivo": ""}).status_code, 422)

    def test_respeta_conjunto(self):
        self.assertEqual(respeta_conjunto('{"elegidos": [{"id": "B"}]}', ["b", "c"]), (True, ["b"]))
        self.assertEqual(respeta_conjunto('{"elegidos": [{"id": "z"}]}', ["b", "c"])[0], False)
        self.assertEqual(respeta_conjunto("no es json", ["b"]), (False, []))
        self.assertEqual(respeta_conjunto('{"elegidos": []}', ["b"]), (True, []))

    def test_el_mensaje_de_sistema_prohibe_inventar(self):
        m = armar_mensajes(PeticionSustitutos(**PEDIDO))
        self.assertIn("No inventes", m[0]["content"])


if __name__ == "__main__":
    unittest.main()
