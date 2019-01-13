import * as tslib_1 from "tslib";
export function increment(tenant, app, counter, key) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        yield fetch(`https://atomiccounter.azurewebsites.net/api/tenant/${tenant}/app/${app}/counter/${counter}/increment?key=${key}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            method: "POST"
        });
    });
}
export function count(tenant, app, counter, key) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        return yield fetch(`https://atomiccounter.azurewebsites.net/api/tenant/${tenant}/app/${app}/counter/${counter}/count?key=${key}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            method: "GET"
        }).then(v => v.json());
    });
}
export function getAuthToken(token) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        const response = yield fetch("https://atomiccounter.azurewebsites.net/.auth/login/google", {
            body: JSON.stringify({
                id_token: token
            }),
            headers: {
                "Content-Type": "application/json"
            },
            method: "POST",
        });
        const ez = yield response.json();
        return ez.authenticationToken;
    });
}
export class AtomicCounterClient {
    constructor(authToken) {
        this.token = authToken;
    }
    createTenant(tenant) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            return yield fetch("https://atomiccounter.azurewebsites.net/api/tenant/" + encodeURI(tenant), {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": this.token
                },
                method: "POST"
            }).then(t => t.json());
        });
    }
    getTenant(tenant) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            return yield fetch("https://atomiccounter.azurewebsites.net/api/tenant/" + encodeURI(tenant), {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": this.token
                },
                method: "GET",
            }).then(t => t.json());
        });
    }
    increment(tenant, app, counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            if (!this.tenants) {
                this.tenants = [yield this.getTenant(tenant)];
            }
            const key = this.tenants.filter(t => t.tenantName === tenant)[0].writeKeys[0];
            yield increment(tenant, app, counter, key);
        });
    }
    count(tenant, app, counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            if (!this.tenants) {
                this.tenants = [yield this.getTenant(tenant)];
            }
            const key = this.tenants.filter(t => t.tenantName === tenant)[0].readKeys[0];
            return yield count(tenant, app, counter, key);
        });
    }
}
//# sourceMappingURL=atomicCounter.js.map