import * as tslib_1 from "tslib";
export function increment(counter, key) {
    return tslib_1.__awaiter(this, void 0, void 0, function* () {
        if (!counterNameIsValid(counter)) {
            // tslint:disable-next-line:no-console
            console.warn("Counter name must be valid before inrementing. Returning without increment.");
            return yield Promise.resolve();
        }
        if (!key || key.trim() === "") {
            // tslint:disable-next-line:no-console
            console.warn("Must provide a write key. Returning without increment.");
            return yield Promise.resolve();
        }
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
        if (!counterNameIsValid(counter)) {
            // tslint:disable-next-line:no-console
            console.warn("Counter name must be valid before counting. Returning 0.");
            return yield Promise.resolve(0);
        }
        if (!key || key.trim() === "") {
            // tslint:disable-next-line:no-console
            console.warn("Must provide a read key. Returning 0.");
            return yield Promise.resolve(0);
        }
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
export function counterNameIsValid(input) {
    return input.length > 3 && input.length < 54 && /[a-z0-9]+/.test(input);
}
export class AtomicCounterClient {
    constructor(authToken) {
        this.token = authToken;
    }
    createCounter(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            if (!counterNameIsValid(counter)) {
                // tslint:disable-next-line:no-console
                console.warn("Counter name must be vaid before creation. Returning null.");
                return yield Promise.resolve(null);
            }
            return yield fetch(`https://atomiccounter.azurewebsites.net/api/counter/${encodeURI(counter)}`, {
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                    "X-ZUMO-AUTH": yield this.token()
                },
                method: "POST"
            })
                .then(t => t.json())
                .catch(() => null);
        });
    }
    getCounter(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            if (!counterNameIsValid(counter)) {
                // tslint:disable-next-line:no-console
                console.warn("Counter name must be valid before retrieving. Returning null.");
                return yield Promise.resolve(null);
            }
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
            if (!counterNameIsValid(counter)) {
                // tslint:disable-next-line:no-console
                console.warn("Counter name must be valid to increment. Returning early.");
                return yield Promise.resolve();
            }
            const meta = yield this.getCounter(counter);
            const key = meta ? meta.writeKeys[0] : null;
            if (!key) {
                // tslint:disable-next-line:no-console
                console.warn("No write keys found. Returning early without incrementing.");
                return Promise.resolve();
            }
            yield increment(counter, key);
        });
    }
    count(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            if (!counterNameIsValid(counter)) {
                // tslint:disable-next-line:no-console
                console.warn("Counter name must be valid to count. Returning 0.");
                return yield Promise.resolve(0);
            }
            const meta = yield this.getCounter(counter);
            const key = meta ? meta.readKeys[0] : null;
            if (!key) {
                // tslint:disable-next-line:no-console
                console.warn("Could not find read keys. Returning 0.");
                return yield Promise.resolve(0);
            }
            return yield count(counter, key).catch(() => 0);
        });
    }
    reset(counter) {
        return tslib_1.__awaiter(this, void 0, void 0, function* () {
            if (!counterNameIsValid(counter)) {
                // tslint:disable-next-line:no-console
                console.warn("Counter name must be valid before resetting. Returning without reset.");
                return yield Promise.resolve();
            }
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