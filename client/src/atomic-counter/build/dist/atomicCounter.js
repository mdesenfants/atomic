import * as tslib_1 from "tslib";
export function increment(key) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        yield fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill/app/bill/counter/bill/increment?key=" + key, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            method: "POST"
        });
    });
}
export function count(key) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        return yield fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill/app/bill/counter/bill/count?key=" + key, {
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
    createTenant() {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            return yield fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill", {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": this.token
                },
                method: "POST"
            }).then(t => t.json());
        });
    }
    getTenant() {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            return yield fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill", {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": this.token
                },
                method: "GET",
            }).then(t => t.json());
        });
    }
    increment() {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            if (!this.tenants) {
                this.tenants = [yield this.getTenant()];
            }
            yield increment(this.tenants[0].writeKeys[0]);
        });
    }
    count() {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            if (!this.tenants) {
                this.tenants = [yield this.getTenant()];
            }
            return yield count(this.tenants[0].readKeys[0]);
        });
    }
}
//# sourceMappingURL=atomicCounter.js.map