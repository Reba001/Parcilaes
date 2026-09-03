// Reemplaza al UpdatePanel + btn_TokenVirtualIngresar_Click / btn_TokenVirtualCancelar_Click
// del code-behind original: valida el OTP por fetch contra /api/token-virtual/validar en vez
// de un postback parcial, y usa un callback JS (por nombre) en lugar del delegado
// "accionRealizar_Click" del server para avisarle a la página anfitriona que siga el flujo.
(function () {
    "use strict";

    function obtenerCallback(nombre) {
        if (!nombre) {
            return null;
        }

        var contexto = window;
        var partes = nombre.split(".");

        for (var i = 0; i < partes.length; i++) {
            if (contexto == null) {
                return null;
            }

            contexto = contexto[partes[i]];
        }

        return typeof contexto === "function" ? contexto : null;
    }

    function mostrarError(contenedor, mensaje) {
        var lbl = contenedor.querySelector("#lbl_MsjError");
        if (!lbl) {
            return;
        }

        lbl.textContent = mensaje || "";
        lbl.style.display = mensaje ? "" : "none";
    }

    function limpiarInput(contenedor) {
        var input = contenedor.querySelector("#txt_TokenVirtual");
        if (input) {
            input.value = "";
            input.focus();
        }
    }

    function validarToken(contenedor) {
        var input = contenedor.querySelector("#txt_TokenVirtual");
        var url = contenedor.getAttribute("data-validar-url");

        if (!input || !url) {
            return;
        }

        mostrarError(contenedor, "");

        fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ codigoOtp: input.value })
        })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("respuesta HTTP " + respuesta.status);
                }
                return respuesta.json();
            })
            .then(function (datos) {
                if (datos.resultado) {
                    var onSuccess = obtenerCallback(contenedor.getAttribute("data-on-success"));
                    if (onSuccess) {
                        onSuccess(contenedor, datos);
                    } else {
                        contenedor.dispatchEvent(new CustomEvent("tokenvirtual:success", { bubbles: true, detail: datos }));
                    }
                } else {
                    mostrarError(contenedor, datos.mensajeResultado || "Número de token incorrecto.");
                    limpiarInput(contenedor);
                }
            })
            .catch(function () {
                mostrarError(contenedor, "Ha ocurrido un error, intentelo mas tarde por favor!");
                limpiarInput(contenedor);
            });
    }

    function cancelarToken(contenedor) {
        var onCancel = obtenerCallback(contenedor.getAttribute("data-on-cancel"));
        if (onCancel) {
            onCancel(contenedor);
        } else {
            contenedor.dispatchEvent(new CustomEvent("tokenvirtual:cancel", { bubbles: true }));
        }
    }

    document.addEventListener("click", function (evento) {
        var boton = evento.target.closest("[data-token-action]");
        if (!boton) {
            return;
        }

        var contenedor = boton.closest(".token-virtual");
        if (!contenedor) {
            return;
        }

        var accion = boton.getAttribute("data-token-action");
        if (accion === "ingresar") {
            evento.preventDefault();
            validarToken(contenedor);
        } else if (accion === "cancelar") {
            evento.preventDefault();
            cancelarToken(contenedor);
        }
    });

    // Enter en el textbox de las variantes que usan <form> (Conexion_Regional*).
    document.addEventListener("submit", function (evento) {
        var formulario = evento.target;
        var contenedor = formulario.closest(".token-virtual");
        if (!contenedor) {
            return;
        }

        evento.preventDefault();
        validarToken(contenedor);
    });
})();
