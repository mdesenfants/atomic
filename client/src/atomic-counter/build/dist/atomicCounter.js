import * as tslib_1 from "tslib";
export function increment(counter, key) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        yield fetch(`https://atomiccounter.azurewebsites.net/api/counter/${counter}/increment?key=${key}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            method: "POST"
        });
    });
}
export function count(counter, key) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        return yield fetch(`https://atomiccounter.azurewebsites.net/api/counter/${counter}/count?key=${key}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            method: "GET"
        }).then(v => v.json());
    });
}
export function getAuthToken(token, provider) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        const response = yield fetch(`https://atomiccounter.azurewebsites.net/.auth/login/${provider}`, {
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
    createCounter(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            return yield fetch(`https://atomiccounter.azurewebsites.net/api/counter/${encodeURI(counter)}`, {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": yield this.token()
                },
                method: "POST"
            }).then(t => t.json());
        });
    }
    getCounter(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            return yield fetch(`https://atomiccounter.azurewebsites.net/api/counter/${encodeURI(counter)}`, {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": yield this.token()
                },
                method: "GET",
            }).then(t => t.json());
        });
    }
    getCounters() {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            return yield fetch('https://atomiccounter.azurewebsites.net/api/counters', {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": yield this.token()
                },
                method: "GET",
            }).then(t => t.json());
        });
    }
    increment(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            const meta = yield this.getCounter(counter);
            const key = meta.writeKeys[0];
            yield increment(counter, key);
        });
    }
    count(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            const meta = yield this.getCounter(counter);
            const key = meta.readKeys[0];
            return yield count(counter, key).catch(() => 0);
        });
    }
    reset(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            yield fetch(`https://atomiccounter.azurewebsites.net/api/counter/${counter}/reset`, {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": yield this.token()
                },
                method: "POST",
            });
        });
    }
}
//# sourceMappingURL=atomicCounter.js.map