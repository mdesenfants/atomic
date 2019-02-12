export interface ICounter {
    counterName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}
export declare function increment(counter: string, key: string): Promise<void>;
export declare function count(counter: string, key: string): Promise<number>;
export declare function counterNameIsValid(input: string): boolean;
export declare class AtomicCounterClient {
    private token;
    constructor(authToken: () => Promise<string>);
    createCounter(counter: string): Promise<ICounter | null>;
    getCounter(counter: string): Promise<ICounter | null>;
    getCounters(): Promise<string[]>;
    increment(counter: string): Promise<void>;
    count(counter: string): Promise<number>;
    reset(counter: string): Promise<void>;
}
