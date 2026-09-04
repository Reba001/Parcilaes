// Reemplaza a btn_GenerarSMS_Click / btn_GenerarEmail_Click del code-behind original: genera el
// OTP por fetch en vez de un postback parcial, y fuerza el panel colapsable a quedar expandido
// cuando hay un mensaje que mostrar (antes lo hacía alternando las clases "collapse"/"collapse in"
// del data-toggle de Bootstrap).
(function () {
    "use strict";

    function mostrarMensaje(contenedor, mensaje, esExito) {
        var lbl = contenedor.querySelector("#lbl_Mensaje");
        if (!lbl) {
            return;
        }

        lbl.textContent = mensaje || "";
        lbl.classList.toggle("cssTokenLblMensajeExito", !!esExito);
    }

    function forzarExpandido(contenedor, expandir) {
        var boton = contenedor.querySelector("#tokensms_btncollapse");
        var panel = contenedor.querySelector("#generartoken_tokensms_show_hide_token");

        if (boton) {
            boton.classList.toggle("collapsed", !expandir);
            boton.setAttribute("aria-expanded", expandir ? "true" : "false");
        }

        if (panel) {
            // "in" es la clase de Bootstrap 3 (igual que en el .ascx original). Si el host ya
            // migró a Bootstrap 4/5, cambiar "in" por "show".
            panel.classList.toggle("in", expandir);
            panel.setAttribute("aria-expanded", expandir ? "true" : "false");
        }
    }

    function generarToken(contenedor, boton, tipoEnvio) {
        var url = contenedor.getAttribute("data-generar-url");
        if (!url) {
            return;
        }

        boton.disabled = true;

        fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ tipoEnvio: tipoEnvio })
        })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("respuesta HTTP " + respuesta.status);
                }
                return respuesta.json();
            })
            .then(function (datos) {
                mostrarMensaje(contenedor, datos.mensajeResultado, datos.resultado);
                forzarExpandido(contenedor, !!(datos.mensajeResultado && datos.mensajeResultado.trim()));
            })
            .catch(function () {
                mostrarMensaje(contenedor, "Ha ocurrido un error, intentelo mas tarde por favor!", false);
                forzarExpandido(contenedor, true);
            })
            .finally(function () {
                boton.disabled = false;
            });
    }

    document.addEventListener("click", function (evento) {
        var boton = evento.target.closest("[data-token-generar]");
        if (!boton) {
            return;
        }

        var contenedor = boton.closest(".generar-token");
        if (!contenedor) {
            return;
        }

        generarToken(contenedor, boton, boton.getAttribute("data-token-generar"));
    });
})();
