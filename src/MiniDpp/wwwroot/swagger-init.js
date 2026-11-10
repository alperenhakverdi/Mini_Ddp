"use strict";

SwaggerUIBundle({
    url: "/openapi/v1.json",
    dom_id: "#swagger-ui",
    deepLinking: true,
    validatorUrl: null,
    defaultModelsExpandDepth: -1,
    supportedSubmitMethods: ["get", "post"],
    presets: [SwaggerUIBundle.presets.apis]
});
