"""Etapa 12: la aplicacion arranca (streamlit.testing). Sin credenciales no muestra datos; con la base de analitica
configurada (ANALITICA_PG_CLAVE y una base con datos del generador sintetico) recorre todas las pestanias sin errores."""
import os
import sys
import unittest
from pathlib import Path
from unittest import mock

from streamlit.testing.v1 import AppTest

APP = str(Path(__file__).resolve().parents[1] / "app" / "principal.py")


class AppSinCredencialesTests(unittest.TestCase):
    def test_sin_usuario_configurado_no_muestra_datos(self):
        with mock.patch.dict(os.environ, {"ANALITICA_APP_USUARIO": "", "ANALITICA_APP_CLAVE": ""}):
            at = AppTest.from_file(APP, default_timeout=60).run()
        self.assertFalse(at.exception)
        self.assertTrue(any("no tiene usuario y contraseña configurados" in e.value for e in at.error))
        self.assertEqual(len(at.tabs), 0)

    def test_con_usuario_configurado_pide_acceso_y_rechaza_una_clave_incorrecta(self):
        with mock.patch.dict(os.environ, {"ANALITICA_APP_USUARIO": "analista", "ANALITICA_APP_CLAVE": "clave-correcta"}):
            at = AppTest.from_file(APP, default_timeout=60).run()
            at.text_input[0].input("analista")
            at.text_input[1].input("otra")
            at.button[0].click().run()
        self.assertFalse(at.exception)
        self.assertTrue(any("incorrectos" in e.value for e in at.error))
        self.assertEqual(len(at.tabs), 0)


@unittest.skipUnless(os.environ.get("ANALITICA_PG_CLAVE"), "sin base de analitica configurada (ANALITICA_PG_CLAVE)")
class AppConDatosTests(unittest.TestCase):
    def test_arranca_con_datos_sinteticos_y_recorre_las_pestanias(self):
        at = AppTest.from_file(APP, default_timeout=180)
        at.session_state["autenticado"] = True
        at.run()
        self.assertFalse(at.exception, at.exception)
        self.assertEqual(len(at.tabs), 4)
        self.assertTrue(any("Origen de los datos: Sintetico" in i.value for i in at.info))
        self.assertTrue(any("Correlación no implica causalidad" in w.value for w in at.warning))


if __name__ == "__main__":
    unittest.main()
